using AmlScreening.Application.Common;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Data;
using AmlScreening.Portal.Helper;
using AmlScreening.Portal.ViewModels;
using Dapper;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AmlScreening.Portal.Services;

public class CustomerKycService
{
    private readonly IAmlRecordRepository
        _amlRepository;

    private readonly AmlScreeningDbContext
        _dbContext;

    private readonly ICurrentUserContext
        _userContext;

    private readonly DropdownHelper
        _dropdownHelper;


    public CustomerKycService(
        IAmlRecordRepository amlRepository,
        AmlScreeningDbContext dbContext,
        ICurrentUserContext userContext,
        DropdownHelper dropdownHelper)
    {
        _amlRepository =
            amlRepository;

        _dbContext =
            dbContext;

        _userContext =
            userContext;

        _dropdownHelper =
            dropdownHelper;
    }


    public async Task<CustomerKycResult> CheckAsync(
        CustomerKycViewModel model,
        CancellationToken cancellationToken)
    {
        if (!_userContext.IsAuthenticated ||
            _userContext.PartnerId <= 0)
        {
            throw new ArgumentException(
                "Invalid partner.");
        }


        // =====================================================
        // 1. LOAD MATCH CONFIGURATION
        // =====================================================

        var settings =
            await GetMatchSettingsAsync(
                cancellationToken);


        // =====================================================
        // 2. CREATE ONE LOG ROW FOR THIS KYC HIT
        // =====================================================

        var log =
            new CustomerKycScreeningLog
            {
                PartnerId =
                    _userContext.PartnerId,

                BranchId =
                    _userContext.BranchId,

                UserId =
                    _userContext.UserId,

                Username =
                    _userContext.Username,

                ScreeningType =
                    model.ScreeningType.Trim(),

                Status =
                    "PROCESSING",

                CreatedDate =
                    DateTime.UtcNow
            };


        _dbContext
            .CustomerKycScreeningLogs
            .Add(log);


        await _dbContext.SaveChangesAsync(
            cancellationToken);


        try
        {
            // =================================================
            // 3. FIND AML CANDIDATES
            // =================================================

            var candidates =
                await _amlRepository
                    .FindKycCandidatesAsync(
                        model.FullName.Trim(),
                        model.ScreeningType,
                        cancellationToken);


            if (candidates.Count > 50)
            {
                throw new ArgumentException(
                    "Too many matches. Enter a more complete name.");
            }

            // =================================================
            // 4. FIRST CHECK NAME SIMILARITY
            // =================================================

            var nameCandidates = candidates.Select(candidate =>
            new
            {
                candidate = candidate,
                NamePercent = GetNamePercent(model.FullName, candidate)
            }).ToList();

            // =====================================================
            // 5. NAME MUST PASS CONFIGURED THRESHOLD
            // =====================================================

            var passedNameCandidates = nameCandidates.Where(x => x.NamePercent >= settings.NameMatchPercent).ToList();
            if (passedNameCandidates.Count == 0)
            {
                log.MatchCount =
                    0;

                log.HighestMatchPercent =
                    nameCandidates.Count == 0
                        ? 0
                        : nameCandidates.Max(
                            x => x.NamePercent);

                log.Status =
                    "COMPLETED_NO_NAME";

                log.CompletedDate =
                    DateTime.UtcNow;


                await _dbContext.SaveChangesAsync(
                    cancellationToken);


                throw new ArgumentException(
                    $"No AML name matched the minimum " +
                    $"{settings.NameMatchPercent:0.##}% " +
                    "name similarity. " +
                    "Risk score was not calculated.");
            }



            // =================================================
            // 6. NAME PASSED - CALCULATE OTHER FIELDS
            // =================================================

            var matchedEvidence =
                passedNameCandidates
                    .Select(x =>
                        CalculateMatch(
                            model,
                            x.candidate,
                            settings,
                            x.NamePercent))
                    .ToList();





            var matchedIds =
                matchedEvidence
                    .Select(x =>
                        x.AmlRecordId)
                    .ToHashSet();


            var matches =
                candidates
                    .Where(x =>
                        matchedIds.Contains(
                            x.Id))
                    .ToList();



            // =================================================
            // 6. GET AML SOURCES
            // =================================================

            var sources =
                matches
                    .Where(x =>
                        !string.IsNullOrWhiteSpace(
                            x.SourceName))
                    .Select(x =>
                        x.SourceName.Trim())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();


            if (sources.Any(
                    x => x.Contains(',')))
            {
                throw new InvalidOperationException(
                    "AML source name contains an unsupported comma.");
            }


            var matchedSources =
                sources.Count == 0
                    ? null
                    : string.Join(
                        ",",
                        sources);


            // =================================================
            // 7. CALL EXISTING RISK SCORE SP
            // =================================================

            var command =
                new CommandDefinition(
                    "dbo.spa_ApiRisk_Calculate",

                    new
                    {
                        MatchedSources =
                            matchedSources,

                        Occupation =
                            model.Occupation.Trim(),

                        SourceOfFunds =
                            model.SourceOfFunds.Trim()
                    },

                    commandType:
                        CommandType.StoredProcedure,

                    commandTimeout:
                        30,

                    cancellationToken:
                        cancellationToken);


            var connection =
                _dbContext.Database
                    .GetDbConnection();


            var risk =
                await connection
                    .QuerySingleAsync<KycRiskResult>(
                        command);


            // =================================================
            // 8. VALIDATE RISK RESULT
            // =================================================

            if (risk.IsPartial ||
                !string.Equals(
                    risk.CalculationMode,
                    "FULL",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Risk calculation is incomplete.");
            }


            var validRiskLevels =
                new[]
                {
                    "LOW",
                    "MEDIUM",
                    "HIGH",
                    "CRITICAL"
                };


            if (!validRiskLevels.Contains(
                    risk.Level,
                    StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Risk calculation returned an invalid risk level.");
            }


            if (string.IsNullOrWhiteSpace(
                    risk.ModelVersion))
            {
                throw new InvalidOperationException(
                    "Risk model version is missing.");
            }


            if (risk.Score < 0)
            {
                throw new InvalidOperationException(
                    "Risk calculation returned an invalid score.");
            }


            // =================================================
            // 9. UPDATE USAGE LOG
            // =================================================

            log.MatchCount =
                matches.Count;


            log.HighestMatchPercent =
                matchedEvidence.Count == 0
                    ? 0
                    : matchedEvidence.Max(
                        x => x.MatchPercent);


            log.RiskScore =
                risk.Score;


            log.RiskLevel =
                risk.Level;


            log.Status =
                "COMPLETED";


            log.CompletedDate =
                DateTime.UtcNow;


            await _dbContext.SaveChangesAsync(
                cancellationToken);


            // =================================================
            // 10. RETURN RESULT
            // =================================================

            return new CustomerKycResult
            {
                Matches =
                    matches,

                // Only show meaningful matches in UI.
                MatchEvidence =
                    matchedEvidence,

                Sources =
                    sources,

                RiskScore =
                    risk.Score,

                RiskLevel =
                    risk.Level,

                ModelVersion =
                    risk.ModelVersion,


                AmlRuleScore =
                    risk.AmlRuleScore,

                AmlWeight =
                    risk.AmlWeight,

                AmlContribution =
                    risk.AmlContribution,


                OccupationRuleScore =
                    risk.OccupationRuleScore,

                OccupationWeight =
                    risk.OccupationWeight,

                OccupationContribution =
                    risk.OccupationContribution,


                SourceOfFundsRuleScore =
                    risk.SourceOfFundsRuleScore,

                SourceOfFundsWeight =
                    risk.SourceOfFundsWeight,

                SourceOfFundsContribution =
                    risk.SourceOfFundsContribution
            };
        }
        catch
        {
            if (log.Status == "PROCESSING")
            {
                log.Status = "FAILED";
                log.CompletedDate = DateTime.UtcNow;


                try
                {
                    await _dbContext.SaveChangesAsync(CancellationToken.None);
                }
                catch
                {
                    // Keep original exception.
                }
            }


            throw;
        }
    }


    // =========================================================
    // DROPDOWNS
    // =========================================================

    public async Task LoadDropdownsAsync(
        CustomerKycViewModel model,
        CancellationToken cancellationToken = default)
    {
        model.CountryOptions =
            await _dropdownHelper.GetCountryListAsync(cancellationToken);



        model.GenderOptions = [
            new SelectListItem
    {
        Value = "MALE",
        Text = "Male"
    },

    new SelectListItem
    {
        Value = "FEMALE",
        Text = "Female"
    },

    new SelectListItem
    {
        Value = "OTHER",
        Text = "Other"
    }
            ];



        model.ScreeningTypeOptions =
            await _dropdownHelper.GetAmlSourceNameAsync(cancellationToken);


        model.OccupationOptions =
            await _dropdownHelper
                .GetDropdownAsync(
                    "OCCUPATION",
                    cancellationToken);


        model.SourceOfFundsOptions =
            await _dropdownHelper
                .GetDropdownAsync(
                    "SOURCE_OF_FUNDS",
                    cancellationToken);
    }


    // =========================================================
    // MATCH CONFIGURATION
    // =========================================================

    private async Task<KycMatchSetting>
        GetMatchSettingsAsync(
            CancellationToken cancellationToken)
    {
        var settings =
            await _dbContext
                .KycMatchSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.IsActive,
                    cancellationToken);


        if (settings == null)
        {
            throw new InvalidOperationException(
                "KYC match configuration is not available.");
        }


        var totalWeight =
            settings.NameWeight
            +
            settings.DateOfBirthWeight
            +
            settings.CountryWeight
            +
            settings.GenderWeight;


        if (totalWeight != 100)
        {
            throw new InvalidOperationException(
                "KYC match weights must total 100.");
        }


        if (settings.NameMatchPercent < 0 ||
            settings.NameMatchPercent > 100)
        {
            throw new InvalidOperationException(
               "Name match threshold must be between 0 and 100.");
        }


        if (settings.StrongMatchThreshold < 0 ||
            settings.StrongMatchThreshold > 100)
        {
            throw new InvalidOperationException(
                "KYC strong match threshold is invalid.");
        }


        if (settings.StrongMatchThreshold <
            settings.ReviewThreshold)
        {
            throw new InvalidOperationException(
                "Strong match threshold cannot be below review threshold.");
        }


        return settings;
    }


    // =========================================================
    // CALCULATE MATCH
    // =========================================================

    private static KycMatchEvidence CalculateMatch(
        CustomerKycViewModel customer,
        AmlRecord aml,
        KycMatchSetting settings,
        decimal namePercent)
    {
        var dobMatched =
      customer.DateOfBirth.HasValue
      &&
      aml.DateOfBirth.HasValue
      &&
      customer.DateOfBirth.Value.Date
      ==
      aml.DateOfBirth.Value.Date;


        var countryMatched =
            Same(
                customer.Country,
                aml.Country);


        var genderMatched =
            Same(
                customer.Gender,
                aml.Gender);


        // Example with configuration:
        //
        // Name match = 100%
        // Name weight = 50
        // Contribution = 50
        //
        // DOB matched = 25
        // Country matched = 15
        // Gender matched = 10

        var matchPercent =
            (
                namePercent
                *
                settings.NameWeight
                /
                100m
            )
            +
            (
                dobMatched
                    ? settings.DateOfBirthWeight
                    : 0
            )
            +
            (
                countryMatched
                    ? settings.CountryWeight
                    : 0
            )
            +
            (
                genderMatched
                    ? settings.GenderWeight
                    : 0
            );


        matchPercent =
            Math.Round(
                matchPercent,
                2);


        var matchStatus =
            matchPercent >=
            settings.StrongMatchThreshold

                ? "STRONG_MATCH"

                : matchPercent >=
                  settings.ReviewThreshold

                    ? "REVIEW"

                    : "NO_MATCH";


        return new KycMatchEvidence
        {
            AmlRecordId =
                aml.Id,

            MatchedName =
                aml.FullName,

            SourceName =
                aml.SourceName,

            ReferenceNo =
                aml.ReferenceNo,

            NameMatchPercent =
                namePercent,

            DateOfBirthMatched =
                dobMatched,

            CountryMatched =
                countryMatched,

            GenderMatched =
                genderMatched,

            MatchPercent =
                matchPercent,

            MatchStatus = matchStatus,
            NameMatchStatus =
        namePercent >= 100
            ? "EXACT_NAME"
            : "FUZZY_NAME"

        };
    }


    // =========================================================
    // NAME MATCH
    // =========================================================

    private static decimal GetNamePercent(
        string customerName,
        AmlRecord aml)
    {
        string customer = AmlNameNormalizer.Normalize(customerName);
        string amlName = AmlNameNormalizer.Normalize(aml.FullName);

        if (customer.Length == 0 || amlName.Length == 0)
        {
            return 0;
        }
        // Exact
        if (customer == amlName)
        {
            return 100;
        }
        // Fuzzy
        var distance = GetLevenshteinDistance(customer, amlName);
        var totalLength = customer.Length + amlName.Length;

        decimal percentage = ((decimal)(totalLength - distance) / totalLength) * 100;
        return Math.Round(percentage, 2);
    }


    // =========================================================
    // TEXT COMPARISON
    // =========================================================

    private static bool Same(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) ||
            string.IsNullOrWhiteSpace(second))
        {
            return false;
        }


        return string.Equals(
            first.Trim(),
            second.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static int GetLevenshteinDistance(
        string first,
        string second)
    {
        var matrix =
            new int[
                first.Length + 1,
                second.Length + 1];


        for (var i = 0;
             i <= first.Length;
             i++)
        {
            matrix[i, 0] = i;
        }


        for (var j = 0;
             j <= second.Length;
             j++)
        {
            matrix[0, j] = j;
        }


        for (var i = 1;
             i <= first.Length;
             i++)
        {
            for (var j = 1;
                 j <= second.Length;
                 j++)
            {
                var cost =
                    first[i - 1] ==
                    second[j - 1]
                        ? 0
                        : 1;


                matrix[i, j] =
                    Math.Min(
                        Math.Min(
                            matrix[i - 1, j] + 1,
                            matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
            }
        }


        return matrix[
            first.Length,
            second.Length];
    }
}
// =============================================================
// RISK SP RESULT
// =============================================================

public class KycRiskResult
{
    public decimal Score { get; set; }

    public string Level { get; set; }
        = string.Empty;

    public string? ModelVersion { get; set; }


    public bool IsPartial { get; set; }

    public string CalculationMode { get; set; }
        = string.Empty;

    public string? Warning { get; set; }


    public decimal? AmlRuleScore { get; set; }

    public decimal? AmlWeight { get; set; }

    public decimal? AmlContribution { get; set; }


    public decimal? OccupationRuleScore { get; set; }

    public decimal? OccupationWeight { get; set; }

    public decimal? OccupationContribution { get; set; }


    public decimal? SourceOfFundsRuleScore { get; set; }

    public decimal? SourceOfFundsWeight { get; set; }

    public decimal? SourceOfFundsContribution { get; set; }
}
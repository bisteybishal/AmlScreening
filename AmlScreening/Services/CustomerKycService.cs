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
    private readonly IAmlRecordRepository _amlRepository;
    private readonly AmlScreeningDbContext _dbContext;
    private readonly ICurrentUserContext _userContext;
    private readonly DropdownHelper _dropdownHelper;


    public CustomerKycService(
        IAmlRecordRepository amlRepository,
        AmlScreeningDbContext dbContext,
        ICurrentUserContext userContext,
        DropdownHelper dropdownHelper)
    {
        _amlRepository = amlRepository;
        _dbContext = dbContext;
        _userContext = userContext;
        _dropdownHelper = dropdownHelper;
    }


    // =========================================================
    // CUSTOMER KYC CHECK
    // =========================================================

    public async Task<CustomerKycResult> CheckAsync(
        CustomerKycViewModel model,
        CancellationToken cancellationToken)
    {
        // =====================================================
        // 1. VALIDATE CURRENT USER
        // =====================================================

        if (!_userContext.IsAuthenticated ||
            _userContext.PartnerId <= 0 ||
            _userContext.UserId <= 0)
        {
            throw new ArgumentException(
                "Invalid authenticated user.");
        }


        if (string.IsNullOrWhiteSpace(model.FullName))
        {
            throw new ArgumentException(
                "Customer name is required.");
        }


        if (string.IsNullOrWhiteSpace(model.ScreeningType))
        {
            throw new ArgumentException(
                "Screening type is required.");
        }


        // =====================================================
        // 2. LOAD MATCH CONFIGURATION
        // =====================================================

        var settings =
            await GetMatchSettingsAsync(
                cancellationToken);


        // =====================================================
        // 3. CREATE SCREENING LOG
        //
        // One KYC request = one row.
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


                // Customer snapshot

                FullName =
                    model.FullName.Trim(),

                DateOfBirth =
                    model.DateOfBirth,

                Country =
                    model.Country?.Trim(),

                Gender =
                    model.Gender?.Trim(),

                Occupation =
                    model.Occupation?.Trim(),

                SourceOfFunds =
                    model.SourceOfFunds?.Trim(),


                Status =
                    "PROCESSING",

                CreatedBy =
                    _userContext.Username,

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
            // 4. FIND POSSIBLE AML CANDIDATES
            // =================================================

            var candidates =
                await _amlRepository
                    .FindKycCandidatesAsync(
                        model.FullName.Trim(),
                        model.ScreeningType.Trim(),
                        cancellationToken);


            if (candidates.Count > 250)
            {
                throw new ArgumentException(
                    "Too many AML candidates. Enter a more complete name.");
            }


            // =================================================
            // 5. CALCULATE NAME SIMILARITY
            // =================================================

            var nameCandidates =
                candidates
                    .Select(candidate => new
                    {
                        Candidate =
                            candidate,

                        NamePercent =
                            GetNamePercent(
                                model.FullName,
                                candidate)
                    })
                    .OrderByDescending(x =>
                        x.NamePercent)
                    .ToList();


            var bestCandidate =
                nameCandidates
                    .FirstOrDefault();


            var highestNamePercent =
                bestCandidate?.NamePercent ?? 0;


            // =================================================
            // 6. SAVE CLOSEST AML RECORD
            //
            // Even NO_MATCH keeps closest record for audit.
            // =================================================

            if (bestCandidate != null)
            {
                log.AmlRecordId =
                    bestCandidate.Candidate.Id;

                log.MatchedName =
                    bestCandidate.Candidate.FullName;

                log.NameMatchPercent =
                    bestCandidate.NamePercent;

                log.HighestMatchPercent =
                    bestCandidate.NamePercent;
            }
            else
            {
                log.NameMatchPercent =
                    0;

                log.HighestMatchPercent =
                    0;
            }


            // =================================================
            // 7. NO MATCH
            //
            // <= configured NameMatchPercent
            //
            // Example:
            //
            // configured = 80
            //
            // 60 -> NO_MATCH
            // 80 -> NO_MATCH
            //
            // No risk calculation.
            // =================================================

            if (bestCandidate == null ||
                highestNamePercent <
                settings.NameMatchPercent)
            {
                log.MatchCount = 0;
                log.RiskScore = null;
                log.RiskLevel = null;
                log.Status = "NO_MATCH";
                log.CompletedDate = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(
                    cancellationToken);
                string message;

                if (bestCandidate == null)
                {
                    message =
                        $"No AML candidate was found for " +
                        $"\"{model.FullName}\". " +
                        "Risk score was not calculated.";
                }
                else
                {
                    message =
                        $"No AML name met the required " +
                        $"{settings.NameMatchPercent:0.##}% threshold. " +
                        $"Closest AML name: " +
                        $"\"{bestCandidate.Candidate.FullName}\". " +
                        $"Name similarity: " +
                        $"{highestNamePercent:0.##}%. " +
                        "Risk score was not calculated.";
                }


                return new CustomerKycResult
                {
                    RiskCalculated =
                        false,

                    RequiresApproval =
                        false,

                    ScreeningStatus =
                        "NO_MATCH",

                    ScreeningMessage = message,

                    Matches =
                        [],

                    MatchEvidence =
                        [],

                    Sources =
                        []
                };
            }


            // =================================================
            // 8. DETERMINE EXACT OR FUZZY
            //
            // > threshold && < 100
            //     = requires checker
            //
            // 100
            //     = exact
            // =================================================

            var requiresApproval = highestNamePercent < 100;


            // =================================================
            // 9. SELECT PASSED NAME CANDIDATES
            // =================================================

            var passedNameCandidates =
                highestNamePercent == 100

                    // Exact name exists.
                    // Only use exact name candidates.
                    ? nameCandidates
                        .Where(x =>
                            x.NamePercent == 100)
                        .ToList()

                    // Fuzzy name.
                    // Use candidates above configured threshold.
                    : nameCandidates
                        .Where(x =>
                            x.NamePercent >=
                            settings.NameMatchPercent)
                        .ToList();


            // =================================================
            // 10. CALCULATE DOB / COUNTRY / GENDER MATCH
            // =================================================

            var matchedEvidence =
                passedNameCandidates
                    .Select(x =>
                        CalculateMatch(
                            model,
                            x.Candidate,
                            settings,
                            x.NamePercent))
                    .ToList();


            if (matchedEvidence.Count == 0)
            {
                throw new InvalidOperationException(
                    "Qualified AML match evidence could not be created.");
            }


            // =================================================
            // 11. GET MATCHED AML RECORDS
            // =================================================

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
            // 12. GET AML SOURCES
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


            if (sources.Count == 0)
            {
                throw new InvalidOperationException(
                    "Matched AML record has no AML source.");
            }


            if (sources.Any(
                    x => x.Contains(',')))
            {
                throw new InvalidOperationException(
                    "AML source name contains an unsupported comma.");
            }


            var matchedSources =
                string.Join(
                    ",",
                    sources);


            // =================================================
            // 13. OPTIONAL OCCUPATION / SOURCE OF FUNDS
            // =================================================

            var occupation =
                string.IsNullOrWhiteSpace(
                    model.Occupation)

                    ? "NOT_PROVIDED"

                    : model.Occupation.Trim();


            var sourceOfFunds =
                string.IsNullOrWhiteSpace(
                    model.SourceOfFunds)

                    ? "NOT_PROVIDED"

                    : model.SourceOfFunds.Trim();


            // =================================================
            // 14. CALCULATE RISK
            // =================================================

            var command =
                new CommandDefinition(
                    "dbo.spa_ApiRisk_Calculate",

                    new
                    {
                        // Add this only when your SP
                        // has @PartnerId parameter.
                        //
                        // PartnerId =
                        //     _userContext.PartnerId,

                        MatchedSources =
                            matchedSources,

                        Occupation =
                            occupation,

                        SourceOfFunds =
                            sourceOfFunds
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
            // 15. VALIDATE RISK RESULT
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


            if (risk.Score < 0 ||
                risk.Score > 100)
            {
                throw new InvalidOperationException(
                    "Risk calculation returned an invalid score.");
            }


            // =================================================
            // 16. UPDATE LOG
            //
            // Fuzzy
            //     -> PENDING_REVIEW
            //
            // Exact
            //     -> COMPLETED
            // =================================================

            log.MatchCount =
                matches.Count;


            log.HighestMatchPercent =
                highestNamePercent;


            log.NameMatchPercent =
                highestNamePercent;


            log.RiskScore =
                risk.Score;


            log.RiskLevel =
                risk.Level;


            log.Status =
                requiresApproval
                    ? "PENDING_REVIEW"
                    : "COMPLETED";


            log.CompletedDate =
                DateTime.UtcNow;


            await _dbContext.SaveChangesAsync(
                cancellationToken);


            // =================================================
            // 17. RETURN RESULT
            // =================================================

            return new CustomerKycResult
            {
                ScreeningStatus =
         requiresApproval
             ? "PENDING_REVIEW"
             : "COMPLETED",

                RequiresApproval =
         requiresApproval,

                RiskCalculated =
         true,

                ScreeningMessage =
         requiresApproval
             ? "Potential AML match found. " +
               "Risk score was calculated. " +
               "Compliance approval is required."
             : "Exact AML name match found. " +
               "Screening completed. " +
               "Compliance approval is not required.",

                Matches =
         matches,

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
            // =================================================
            // ONLY TECHNICAL FAILURE SHOULD BECOME FAILED
            //
            // NO_MATCH returns normally before this point.
            // =================================================

            if (log.Status == "PROCESSING")
            {
                log.Status =
                    "FAILED";

                log.CompletedDate =
                    DateTime.UtcNow;


                try
                {
                    await _dbContext.SaveChangesAsync(
                        CancellationToken.None);
                }
                catch
                {
                    // Never hide the original exception.
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
            await _dropdownHelper
                .GetCountryListAsync(
                    cancellationToken);


        model.GenderOptions =
        [
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
            await _dropdownHelper
                .GetAmlSourceNameAsync(
                    cancellationToken);


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
    // MATCH SETTINGS
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


        // NameMatchPercent must remain below 100
        // because 100 is reserved for exact matching.

        if (settings.NameMatchPercent < 0 ||
            settings.NameMatchPercent >= 100)
        {
            throw new InvalidOperationException(
                "Name match threshold must be between 0 and less than 100.");
        }


        if (settings.ReviewThreshold < 0 ||
            settings.ReviewThreshold > 100)
        {
            throw new InvalidOperationException(
                "KYC review threshold is invalid.");
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
    // CALCULATE CUSTOMER MATCH
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

            NameMatchStatus =
                namePercent == 100
                    ? "EXACT_NAME"
                    : "FUZZY_NAME",

            DateOfBirthMatched =
                dobMatched,

            CountryMatched =
                countryMatched,

            GenderMatched =
                genderMatched,

            MatchPercent =
                matchPercent,

            MatchStatus =
                matchStatus
        };
    }


    // =========================================================
    // NAME MATCH
    // =========================================================

    private static decimal GetNamePercent(
        string customerName,
        AmlRecord aml)
    {
        var customer =
            AmlNameNormalizer.Normalize(
                customerName);


        var amlName =
            !string.IsNullOrWhiteSpace(
                aml.NormalizedName)

                ? aml.NormalizedName

                : AmlNameNormalizer.Normalize(
                    aml.FullName);


        if (string.IsNullOrWhiteSpace(customer) ||
            string.IsNullOrWhiteSpace(amlName))
        {
            return 0;
        }


        return CalculateNameSimilarity(
            customer,
            amlName);
    }


    // =========================================================
    // LEVENSHTEIN NAME %
    // =========================================================

    private static decimal CalculateNameSimilarity(
        string first,
        string second)
    {
        if (string.IsNullOrWhiteSpace(first) ||
            string.IsNullOrWhiteSpace(second))
        {
            return 0;
        }


        if (string.Equals(
                first,
                second,
                StringComparison.Ordinal))
        {
            return 100;
        }


        var distance =
            GetLevenshteinDistance(
                first,
                second);


        var totalLength =
            first.Length +
            second.Length;


        if (totalLength == 0)
        {
            return 0;
        }


        var percentage =
            ((decimal)
                (totalLength - distance)
                / totalLength)
            * 100m;


        return Math.Round(
            percentage,
            2);
    }


    // =========================================================
    // OTHER TEXT MATCH
    // =========================================================

    private static bool Same(
        string? first,
        string? second)
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


    // =========================================================
    // LEVENSHTEIN DISTANCE
    // =========================================================

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
            matrix[i, 0] =
                i;
        }


        for (var j = 0;
             j <= second.Length;
             j++)
        {
            matrix[0, j] =
                j;
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

                        matrix[i - 1, j - 1] +
                        cost);
            }
        }


        return matrix[
            first.Length,
            second.Length];
    }
}


// =============================================================
// RISK STORED PROCEDURE RESULT
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
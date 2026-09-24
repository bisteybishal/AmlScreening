using AmlScreening.Application.Common;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AmlScreening.Infrastructure.Services;

public sealed class AmlWorkflowService : IAmlWorkflowService
{
    private readonly AmlScreeningDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly IRiskScoringService _riskScoring;
    private readonly IComplianceDashboardRepository _complianceDashboardRepository;

    public AmlWorkflowService(
        AmlScreeningDbContext db,
        ICurrentUserContext currentUser,
        IRiskScoringService riskScoring,
        IComplianceDashboardRepository complianceDashboardRepository)
    {
        _db = db;
        _currentUser = currentUser;
        _riskScoring = riskScoring;
        _complianceDashboardRepository = complianceDashboardRepository;
    }


    public async Task<ComplianceDashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await _complianceDashboardRepository.GetSummaryAsync(
       _currentUser.PartnerId,
       _currentUser.HasPermission(
           AppPermissions.Transactions.View),

       _currentUser.HasPermission(
           AppPermissions.Alerts.View),

       _currentUser.HasPermission(
           AppPermissions.Cases.View),

       cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync() =>
        await _db.Customers.AsNoTracking()
            .Where(x => x.PartnerId == _currentUser.PartnerId)
            .OrderBy(x => x.FullName)
            .ToListAsync();

    public Task<Customer?> GetCustomerAsync(int id) =>
        _db.Customers.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && x.PartnerId == _currentUser.PartnerId);

    public async Task<int> CreateCustomerAsync(Customer customer)
    {
        customer.PartnerId = _currentUser.PartnerId;
        customer.CreatedBy = _currentUser.Username;
        customer.CreatedDate = DateTime.UtcNow;
        customer.RiskScore = CalculateCustomerRisk(customer);
        customer.RiskLevel = RiskLevel(customer.RiskScore);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        AddAudit("Create", "Customer", customer.Id,
            $"Created customer {customer.CustomerReference}.");
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return customer.Id;
    }

    public async Task<bool> UpdateCustomerAsync(Customer customer)
    {
        var existing = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == customer.Id && x.PartnerId == _currentUser.PartnerId);
        if (existing is null) return false;

        customer.PartnerId = _currentUser.PartnerId;
        customer.CreatedBy = existing.CreatedBy;
        customer.CreatedDate = existing.CreatedDate;
        customer.UpdatedBy = _currentUser.Username;
        customer.UpdatedDate = DateTime.UtcNow;
        customer.RiskScore = CalculateCustomerRisk(customer);
        customer.RiskLevel = RiskLevel(customer.RiskScore);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Customers.Update(customer);
            _db.Entry(customer).Property(x => x.RowVersion).OriginalValue = customer.RowVersion;
            await _db.SaveChangesAsync();
            AddAudit("Update", "Customer", customer.Id,
                $"Updated customer {customer.CustomerReference}.");
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<bool> DeleteCustomerAsync(int id)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x =>
            x.Id == id && x.PartnerId == _currentUser.PartnerId);
        if (customer is null) return false;

        customer.DeleteFg = "Y";
        customer.DeleteDate = DateTime.UtcNow;
        customer.DeleteBy = _currentUser.Username;
        customer.IsActive = false;
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.SaveChangesAsync();
        AddAudit("Delete", "Customer", id,
            $"Soft deleted customer {customer.CustomerReference}.");
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    public async Task<IReadOnlyList<AmlTransaction>> GetTransactionsAsync() =>
        await _db.Transactions.AsNoTracking()
            .Where(x => x.PartnerId == _currentUser.PartnerId)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .Take(500)
            .ToListAsync();

    public async Task<AmlTransactionDetails?> GetTransactionDetailsAsync(int id)
    {
        var transaction = await _db.Transactions.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && x.PartnerId == _currentUser.PartnerId);
        if (transaction is null) return null;

        var assessment = await _db.RiskAssessments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TransactionId == id);

        return new AmlTransactionDetails
        {
            Transaction = transaction,
            Customer = await _db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == transaction.CustomerId),
            RiskAssessment = assessment,
            Decision = await _db.AmlDecisions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TransactionId == id),
            Matches = await _db.ScreeningMatches.AsNoTracking()
                .Where(x => x.TransactionId == id)
                .OrderByDescending(x => x.MatchScore)
                .ToListAsync(),
            Factors = assessment is null
                ? Array.Empty<RiskAssessmentFactor>()
                : await _db.RiskAssessmentFactors.AsNoTracking()
                    .Where(x => x.RiskAssessmentId == assessment.Id)
                    .OrderBy(x => x.Id)
                    .ToListAsync()
        };
    }

    public async Task<int> CreateTransactionAsync(AmlTransaction transaction)
    {
        var customerExists = await _db.Customers.AsNoTracking().AnyAsync(x =>
            x.Id == transaction.CustomerId && x.PartnerId == _currentUser.PartnerId);
        if (!customerExists)
            throw new InvalidOperationException("The selected customer does not exist for this client.");

        transaction.PartnerId = _currentUser.PartnerId;
        transaction.CreatedBy = _currentUser.Username;
        transaction.CreatedDate = DateTime.UtcNow;
        transaction.Status = "NEW";
        transaction.CorrelationId = Guid.NewGuid().ToString("N");

        await using var databaseTransaction = await _db.Database.BeginTransactionAsync();
        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();
        AddAudit("Create", "AmlTransaction", transaction.Id,
            $"Created transaction {transaction.TransactionReference}.");
        await _db.SaveChangesAsync();
        await databaseTransaction.CommitAsync();
        return transaction.Id;
    }

    public async Task<AmlEvaluationResult?> EvaluateAsync(int transactionId)
    {
        var transaction = await _db.Transactions.FirstOrDefaultAsync(x =>
            x.Id == transactionId && x.PartnerId == _currentUser.PartnerId);
        if (transaction is null) return null;

        var existingDecision = await _db.AmlDecisions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TransactionId == transactionId);
        if (existingDecision is not null)
            return await LoadExistingResultAsync(transaction, existingDecision);

        var customer = await _db.Customers.FirstOrDefaultAsync(x =>
            x.Id == transaction.CustomerId && x.PartnerId == _currentUser.PartnerId);
        if (customer is null)
            throw new InvalidOperationException("The transaction customer no longer exists.");

        await using var databaseTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var reasons = new List<string>();
            var matches = await ScreenPartiesAsync(transaction, customer);
            if (matches.Count > 0)
                _db.ScreeningMatches.AddRange(matches);

            var screeningRisk = matches.Count == 0
                ? 0
                : (int)Math.Round(matches.Max(x => x.MatchScore));
            var screeningStatus = matches.Any(x => x.MatchStatus == "CONFIRMED_MATCH")
                ? "CONFIRMED_MATCH"
                : matches.Count > 0 ? "POTENTIAL_MATCH" : "CLEAR";

            if (screeningStatus != "CLEAR")
                reasons.Add($"{screeningStatus.Replace('_', ' ')} found during AML screening.");

            var transactionRuleRisk = await CalculateTransactionRiskAsync(transaction, reasons);
            var scoring = await _riskScoring.CalculateAsync(new RiskScoringInput
            {
                IsPep = customer.IsPep,
                Occupation = customer.Occupation,
                SourceOfFunds = customer.SourceOfFunds,
                Amount = transaction.Amount,
                Currency = transaction.Currency,
                ReceivingCountry = transaction.DestinationCountry,
                PurposeCode = transaction.PurposeCode,
                RegisteredReceiverCount = transaction.RegisteredReceiverCount,
                RelationshipCode = transaction.RelationshipCode,
                ScreeningRiskScore = screeningRisk,
                AdditionalTransactionRiskScore = transactionRuleRisk.Score
            });

            foreach (var factor in scoring.Factors.Where(item => item.RawScore > 0))
                reasons.Add(factor.Explanation);

            customer.RiskScore = scoring.CustomerRiskScore;
            customer.RiskLevel = RiskLevel(scoring.CustomerRiskScore);
            customer.UpdatedBy = "AML Engine";
            customer.UpdatedDate = DateTime.UtcNow;

            var decisionValue = Decide(
                screeningStatus,
                scoring.RecommendedDecision,
                transactionRuleRisk.HighestAction);
            if (reasons.Count == 0)
                reasons.Add("No screening match or active compliance rule was triggered.");

            var reasonText = JoinAndTruncate(reasons, 2000);
            var factorText = JoinAndTruncate(
                scoring.Factors.Select(item => item.Explanation), 2000);

            var assessment = new RiskAssessment
            {
                PartnerId = _currentUser.PartnerId,
                TransactionId = transaction.Id,
                CustomerRiskScore = scoring.CustomerRiskScore,
                TransactionRiskScore = scoring.TransactionRiskScore,
                ScreeningRiskScore = screeningRisk,
                OverallRiskScore = scoring.OverallRiskScore,
                RiskLevel = scoring.RiskLevel,
                ModelVersion = scoring.ModelVersion,
                Factors = factorText,
                CreatedBy = "AML Engine",
                CalculatedAt = DateTime.UtcNow
            };
            _db.RiskAssessments.Add(assessment);
            await _db.SaveChangesAsync();

            var assessmentFactors = scoring.Factors.Select(factor =>
                new RiskAssessmentFactor
                {
                    PartnerId = _currentUser.PartnerId,
                    RiskAssessmentId = assessment.Id,
                    FactorCode = factor.FactorCode,
                    InputValue = factor.InputValue,
                    RawScore = factor.RawScore,
                    WeightPercent = factor.WeightPercent,
                    WeightedScore = factor.WeightedScore,
                    Explanation = factor.Explanation,
                    RuleVersion = factor.RuleVersion,
                    CreatedBy = "AML Engine"
                }).ToList();
            _db.RiskAssessmentFactors.AddRange(assessmentFactors);

            var riskRuleVersions = scoring.Factors
                .Select(item => $"{item.FactorCode}:v{item.RuleVersion}")
                .Distinct();
            var appliedVersions = transactionRuleRisk.RuleVersions == "none"
                ? riskRuleVersions
                : riskRuleVersions.Append(transactionRuleRisk.RuleVersions);

            var decision = new AmlDecision
            {
                PartnerId = _currentUser.PartnerId,
                TransactionId = transaction.Id,
                RiskAssessmentId = assessment.Id,
                CorrelationId = transaction.CorrelationId ?? Guid.NewGuid().ToString("N"),
                Decision = decisionValue,
                Reasons = reasonText,
                RuleVersions = JoinAndTruncate(appliedVersions, 1000),
                ModelVersion = scoring.ModelVersion,
                ListVersions = matches.Count == 0
                    ? "manual-v1"
                    : string.Join(", ", matches.Select(x => x.ListVersion).Distinct()),
                CreatedBy = "AML Engine"
            };
            _db.AmlDecisions.Add(decision);

            transaction.Decision = decisionValue;
            transaction.Status = decisionValue == "ALLOW" ? "CLEARED" : "HOLD";
            transaction.RiskScore = scoring.OverallRiskScore;
            transaction.RiskLevel = scoring.RiskLevel;
            transaction.ScreeningStatus = screeningStatus;
            transaction.DecisionReasons = decision.Reasons;
            transaction.EvaluatedDate = DateTime.UtcNow;
            transaction.UpdatedBy = "AML Engine";
            transaction.UpdatedDate = DateTime.UtcNow;

            ComplianceAlert? alert = null;
            ComplianceCase? complianceCase = null;
            if (decisionValue is "REVIEW" or "BLOCK")
            {
                alert = new ComplianceAlert
                {
                    PartnerId = _currentUser.PartnerId,
                    AlertReference = Reference("ALT"),
                    TransactionId = transaction.Id,
                    RiskAssessmentId = assessment.Id,
                    Severity = decisionValue == "BLOCK" ? "CRITICAL" : scoring.RiskLevel,
                    Reason = decision.Reasons,
                    Status = "NEW",
                    CreatedBy = "AML Engine"
                };
                _db.ComplianceAlerts.Add(alert);
                await _db.SaveChangesAsync();

                complianceCase = new ComplianceCase
                {
                    PartnerId = _currentUser.PartnerId,
                    CaseReference = Reference("CASE"),
                    AlertId = alert.Id,
                    TransactionId = transaction.Id,
                    Status = "NEW",
                    CreatedBy = "AML Engine"
                };
                _db.ComplianceCases.Add(complianceCase);
            }

            AddAudit("Evaluate", "AmlDecision", transaction.Id,
                $"{transaction.TransactionReference}: {decisionValue}; score {scoring.OverallRiskScore}; correlation {decision.CorrelationId}.");
            await _db.SaveChangesAsync();
            await databaseTransaction.CommitAsync();

            return new AmlEvaluationResult
            {
                Transaction = transaction,
                RiskAssessment = assessment,
                Decision = decision,
                Matches = matches,
                Factors = assessmentFactors,
                Alert = alert,
                Case = complianceCase
            };
        }
        catch
        {
            await databaseTransaction.RollbackAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<ComplianceAlert>> GetAlertsAsync() =>
        await _db.ComplianceAlerts.AsNoTracking()
            .Where(x => x.PartnerId == _currentUser.PartnerId)
            .OrderByDescending(x => x.CreatedDate)
            .Take(500)
            .ToListAsync();

    public Task<ComplianceAlert?> GetAlertAsync(int id) =>
        _db.ComplianceAlerts.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && x.PartnerId == _currentUser.PartnerId);

    public async Task<bool> UpdateAlertAsync(int id, string status, string? resolution)
    {
        var normalizedStatus = status.Trim().ToUpperInvariant();
        if (normalizedStatus is "RESOLVED" or "CLOSED" && string.IsNullOrWhiteSpace(resolution))
            throw new InvalidOperationException("A resolution is required before an alert can be closed.");
        var alert = await _db.ComplianceAlerts.FirstOrDefaultAsync(x =>
            x.Id == id && x.PartnerId == _currentUser.PartnerId);
        if (alert is null) return false;

        alert.Status = normalizedStatus;
        alert.Resolution = resolution?.Trim();
        alert.AssignedUserId ??= _currentUser.UserId;
        alert.UpdatedBy = _currentUser.Username;
        alert.UpdatedDate = DateTime.UtcNow;
        if (alert.Status is "RESOLVED" or "CLOSED") alert.ResolvedDate = DateTime.UtcNow;
        AddAudit("Update", "ComplianceAlert", alert.Id,
            $"Changed alert {alert.AlertReference} to {alert.Status}.");
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<ComplianceCase>> GetCasesAsync() =>
        await _db.ComplianceCases.AsNoTracking()
            .Where(x => x.PartnerId == _currentUser.PartnerId)
            .OrderByDescending(x => x.CreatedDate)
            .Take(500)
            .ToListAsync();

    public Task<ComplianceCase?> GetCaseAsync(int id) =>
        _db.ComplianceCases.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && x.PartnerId == _currentUser.PartnerId);

    public async Task<bool> UpdateCaseAsync(
        int id,
        string status,
        string? disposition,
        string? notes)
    {
        var normalizedStatus = status.Trim().ToUpperInvariant();
        if (normalizedStatus is "CLEARED" or "REJECTED" or "STR_SAR" or "CLOSED" &&
            string.IsNullOrWhiteSpace(disposition))
            throw new InvalidOperationException("A disposition is required for the selected case status.");
        var complianceCase = await _db.ComplianceCases.FirstOrDefaultAsync(x =>
            x.Id == id && x.PartnerId == _currentUser.PartnerId);
        if (complianceCase is null) return false;

        complianceCase.Status = normalizedStatus;
        complianceCase.Disposition = disposition?.Trim();
        complianceCase.Notes = notes?.Trim();
        complianceCase.AssignedUserId ??= _currentUser.UserId;
        complianceCase.UpdatedBy = _currentUser.Username;
        complianceCase.UpdatedDate = DateTime.UtcNow;
        if (complianceCase.Status == "CLOSED") complianceCase.ClosedDate = DateTime.UtcNow;
        AddAudit("Update", "ComplianceCase", complianceCase.Id,
            $"Changed case {complianceCase.CaseReference} to {complianceCase.Status}.");
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<List<ScreeningMatch>> ScreenPartiesAsync(
        AmlTransaction transaction,
        Customer customer)
    {
        var subjects = new[]
        {
            transaction.SenderName,
            transaction.ReceiverName
        };
        var matches = new List<ScreeningMatch>();


        foreach (var subject in subjects.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var records = await GetScreeningCandidatesAsync(subject);
            foreach (var record in records)
            {
                var score = Similarity(subject, record.FullName);
                if (string.Equals(subject, transaction.SenderName, StringComparison.OrdinalIgnoreCase))
                {
                    if (record.DateOfBirth.HasValue && customer.DateOfBirth == record.DateOfBirth)
                        score += 10;
                    if (!string.IsNullOrWhiteSpace(record.Nationality) &&
                        string.Equals(record.Nationality, customer.Nationality, StringComparison.OrdinalIgnoreCase))
                        score += 5;
                }

                score = Math.Min(100, score);
                if (score < 75) continue;
                matches.Add(new ScreeningMatch
                {
                    PartnerId = _currentUser.PartnerId,
                    TransactionId = transaction.Id,
                    AmlRecordId = record.Id,
                    SubjectName = subject,
                    MatchedName = record.FullName,
                    MatchScore = score,
                    MatchStatus = "POTENTIAL_MATCH", // A name score cannot establish identity.
                    SourceName = record.SourceName,
                    ListVersion = "manual-v1",
                    ComparedFields = "Name, DateOfBirth, Nationality",
                    CreatedBy = "AML Engine"
                });
            }
        }

        return matches;
    }

    private async Task<TransactionRiskResult> CalculateTransactionRiskAsync(
        AmlTransaction transaction,
        List<string> reasons)
    {
        var now = DateTime.UtcNow;
        var rules = await _db.ComplianceRules.AsNoTracking()
            .Where(x => x.PartnerId == _currentUser.PartnerId && x.IsActive &&
                x.ApprovalStatus == "APPROVED" && x.EffectiveFrom <= now &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo >= now))
            .OrderBy(x => x.Priority)
            .ToListAsync();

        var score = 0;
        var highestAction = "Allow";
        var versions = new List<string>();
        foreach (var rule in rules)
        {
            var triggered = false;
            if (rule.RuleType.Equals("AmountThreshold", StringComparison.OrdinalIgnoreCase) &&
                rule.ThresholdAmount.HasValue && transaction.Amount >= rule.ThresholdAmount &&
                (string.IsNullOrWhiteSpace(rule.Currency) ||
                 rule.Currency.Equals(transaction.Currency, StringComparison.OrdinalIgnoreCase)))
            {
                triggered = true;
            }
            else if (rule.RuleType.Equals("Velocity", StringComparison.OrdinalIgnoreCase) &&
                     rule.LookbackMinutes.HasValue && rule.TransactionCount.HasValue)
            {
                var from = transaction.TransactionDate.AddMinutes(-rule.LookbackMinutes.Value);
                var count = await _db.Transactions.AsNoTracking().CountAsync(x =>
                    x.PartnerId == _currentUser.PartnerId &&
                    x.CustomerId == transaction.CustomerId &&
                    x.TransactionDate >= from && x.TransactionDate <= transaction.TransactionDate);
                triggered = count >= rule.TransactionCount.Value;
            }

            if (!triggered) continue;
            score = Math.Clamp(score + rule.RiskScore, 0, 100);
            highestAction = HigherAction(highestAction, rule.Action);
            versions.Add($"{rule.RuleCode}:v{rule.RuleVersion}");
            reasons.Add($"Rule {rule.RuleCode} triggered: {rule.RuleName}.");
        }

        return new TransactionRiskResult(score, highestAction,
            versions.Count == 0 ? "none" : string.Join(", ", versions));
    }

    private async Task<AmlEvaluationResult> LoadExistingResultAsync(
        AmlTransaction transaction,
        AmlDecision decision)
    {
        var assessment = await _db.RiskAssessments.AsNoTracking()
            .SingleAsync(x => x.Id == decision.RiskAssessmentId);
        var matches = await _db.ScreeningMatches.AsNoTracking()
            .Where(x => x.TransactionId == transaction.Id)
            .ToListAsync();
        var factors = await _db.RiskAssessmentFactors.AsNoTracking()
            .Where(x => x.RiskAssessmentId == assessment.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var alert = await _db.ComplianceAlerts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TransactionId == transaction.Id);
        var complianceCase = alert is null ? null : await _db.ComplianceCases.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AlertId == alert.Id);
        return new AmlEvaluationResult
        {
            Transaction = transaction,
            RiskAssessment = assessment,
            Decision = decision,
            Matches = matches,
            Factors = factors,
            Alert = alert,
            Case = complianceCase
        };
    }

    private static int CalculateCustomerRisk(Customer customer)
    {
        var score = 0;
        if (customer.IsPep) score += 50;
        if (string.IsNullOrWhiteSpace(customer.SourceOfFunds)) score += 15;
        if (string.IsNullOrWhiteSpace(customer.Occupation)) score += 10;
        return Math.Clamp(score, 0, 100);
    }

    private static string Decide(
        string screeningStatus,
        string riskBandDecision,
        string ruleAction)
    {
        if (screeningStatus == "CONFIRMED_MATCH" ||
            ruleAction.Equals("Block", StringComparison.OrdinalIgnoreCase))
            return "BLOCK";

        var normalizedRuleDecision = ruleAction.ToUpperInvariant() switch
        {
            "BLOCK" => "BLOCK",
            "REVIEW" or "HOLD" or "ALERT" => "REVIEW",
            _ => "ALLOW"
        };
        var finalDecision = HigherAction(
            riskBandDecision.ToUpperInvariant(),
            normalizedRuleDecision).ToUpperInvariant();
        return screeningStatus == "POTENTIAL_MATCH"
            ? HigherAction(finalDecision, "REVIEW").ToUpperInvariant()
            : finalDecision;
    }

    private static string HigherAction(string current, string candidate)
    {
        static int Rank(string action) => action.ToUpperInvariant() switch
        {
            "BLOCK" => 3,
            "REVIEW" or "ALERT" or "HOLD" => 2,
            _ => 1
        };
        return Rank(candidate) > Rank(current) ? candidate : current;
    }

    private static string RiskLevel(int score) => score switch
    {
        >= 80 => "CRITICAL",
        >= 60 => "HIGH",
        >= 30 => "MEDIUM",
        _ => "LOW"
    };

    private static decimal Similarity(
    string first,
    string second)
    {
        var left =
            AmlNameNormalizer.Normalize(
                first);

        var right =
            AmlNameNormalizer.Normalize(
                second);


        if (left.Length == 0
            ||
            right.Length == 0)
        {
            return 0;
        }


        if (left == right)
        {
            return 100;
        }


        var distance =
            Levenshtein(
                left,
                right);


        return Math.Round(
            (
                1m
                -
                distance
                /
                (decimal)Math.Max(
                    left.Length,
                    right.Length)
            )
            *
            100m,
            2);
    }

    
    private static int Levenshtein(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }
            previous = current;
        }
        return previous[right.Length];
    }

    private void AddAudit(string action, string entityType, int entityId, string details) =>
        _db.AuditLogs.Add(new AuditLog
        {
            PartnerId = _currentUser.PartnerId,
            UserId = _currentUser.UserId,
            Username = _currentUser.Username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Details = details,
            CreatedDate = DateTime.UtcNow
        });

    private static string Reference(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..Math.Min(40, prefix.Length + 29)];

    private static string JoinAndTruncate(IEnumerable<string> values, int maximumLength)
    {
        var text = string.Join(" | ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        return text.Length <= maximumLength ? text : text[..maximumLength];
    }

    private sealed record TransactionRiskResult(int Score, string HighestAction, string RuleVersions);
    private async Task<List<AmlRecord>> GetScreeningCandidatesAsync(string name)
    {
        if (!_currentUser.IsAuthenticated ||
            _currentUser.UserId <= 0 ||
            _currentUser.PartnerId <= 0 ||
            _currentUser.BranchId <= 0)
        {
            throw new UnauthorizedAccessException(
                "A valid partner, branch and user login is required.");
        }

        var normalizedName = AmlNameNormalizer.Normalize(name);

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return [];
        }

        var candidates = await _db.AmlRecords
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.DeleteFg == "N" &&
                (
                 _currentUser.IsAdmin ||
                    x.IsGlobal ||
                    x.PartnerId == _currentUser.PartnerId
                ))
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Id)
            .Take(10001)
            .Select(x => new AmlRecord
            {
                Id = x.Id,
                PartnerId = x.PartnerId,
                BranchId = x.BranchId,
                CreatedByUserId = x.CreatedByUserId,
                IsGlobal = x.IsGlobal,
                IsActive = x.IsActive,
                FullName = x.FullName,
                DateOfBirth = x.DateOfBirth,
                Nationality = x.Nationality,
                SourceName = x.SourceName,
                LastSyncDate = x.LastSyncDate,
                SourceUpdatedDate = x.SourceUpdatedDate
            })
            .ToListAsync();

        if (candidates.Count > 10000)
        {
            throw new InvalidOperationException(
                "AML candidate limit exceeded. Screening was not completed; " +
                "do not treat this result as clear.");
        }

        return candidates;
    }
}

using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities;
using AmlScreening.Domain.Entities.Compliance;
using AmlScreening.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AmlScreening.Infrastructure.Services;

public sealed class AmlWorkflowService : IAmlWorkflowService
{
    private const string ModelVersion = "risk-v1.0";
    private readonly AmlScreeningDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public AmlWorkflowService(
        AmlScreeningDbContext db,
        ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersAsync() =>
        await _db.Customers.AsNoTracking()
            .Where(x => x.ClientId == _currentUser.ClientId)
            .OrderBy(x => x.FullName)
            .ToListAsync();

    public Task<Customer?> GetCustomerAsync(int id) =>
        _db.Customers.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && x.ClientId == _currentUser.ClientId);

    public async Task<int> CreateCustomerAsync(Customer customer)
    {
        customer.ClientId = _currentUser.ClientId;
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
            x.Id == customer.Id && x.ClientId == _currentUser.ClientId);
        if (existing is null) return false;

        customer.ClientId = _currentUser.ClientId;
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
            x.Id == id && x.ClientId == _currentUser.ClientId);
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
            .Where(x => x.ClientId == _currentUser.ClientId)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .Take(500)
            .ToListAsync();

    public async Task<AmlTransactionDetails?> GetTransactionDetailsAsync(int id)
    {
        var transaction = await _db.Transactions.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && x.ClientId == _currentUser.ClientId);
        if (transaction is null) return null;

        return new AmlTransactionDetails
        {
            Transaction = transaction,
            Customer = await _db.Customers.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == transaction.CustomerId),
            RiskAssessment = await _db.RiskAssessments.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TransactionId == id),
            Decision = await _db.AmlDecisions.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TransactionId == id),
            Matches = await _db.ScreeningMatches.AsNoTracking()
                .Where(x => x.TransactionId == id)
                .OrderByDescending(x => x.MatchScore)
                .ToListAsync()
        };
    }

    public async Task<int> CreateTransactionAsync(AmlTransaction transaction)
    {
        var customerExists = await _db.Customers.AsNoTracking().AnyAsync(x =>
            x.Id == transaction.CustomerId && x.ClientId == _currentUser.ClientId);
        if (!customerExists)
            throw new InvalidOperationException("The selected customer does not exist for this client.");

        transaction.ClientId = _currentUser.ClientId;
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
            x.Id == transactionId && x.ClientId == _currentUser.ClientId);
        if (transaction is null) return null;

        var existingDecision = await _db.AmlDecisions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TransactionId == transactionId);
        if (existingDecision is not null)
            return await LoadExistingResultAsync(transaction, existingDecision);

        var customer = await _db.Customers.FirstOrDefaultAsync(x =>
            x.Id == transaction.CustomerId && x.ClientId == _currentUser.ClientId);
        if (customer is null)
            throw new InvalidOperationException("The transaction customer no longer exists.");

        await using var databaseTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var reasons = new List<string>();
            var matches = await ScreenPartiesAsync(transaction, customer);
            if (matches.Count > 0)
                _db.ScreeningMatches.AddRange(matches);

            var customerRisk = CalculateCustomerRisk(customer);
            customer.RiskScore = customerRisk;
            customer.RiskLevel = RiskLevel(customerRisk);
            customer.UpdatedBy = "AML Engine";
            customer.UpdatedDate = DateTime.UtcNow;

            var screeningRisk = matches.Count == 0
                ? 0
                : (int)Math.Round(matches.Max(x => x.MatchScore));
            var screeningStatus = matches.Any(x => x.MatchStatus == "CONFIRMED_MATCH")
                ? "CONFIRMED_MATCH"
                : matches.Count > 0 ? "POTENTIAL_MATCH" : "CLEAR";

            if (screeningStatus != "CLEAR")
                reasons.Add($"{screeningStatus.Replace('_', ' ')} found during AML screening.");

            var transactionRisk = await CalculateTransactionRiskAsync(transaction, reasons);
            var overallRisk = Math.Clamp((int)Math.Round(
                customerRisk * 0.40m + transactionRisk.Score * 0.40m + screeningRisk * 0.20m), 0, 100);
            var riskLevel = RiskLevel(overallRisk);

            var decisionValue = Decide(
                screeningStatus,
                overallRisk,
                transactionRisk.HighestAction);
            if (reasons.Count == 0)
                reasons.Add("No screening match or active compliance rule was triggered.");

            var assessment = new RiskAssessment
            {
                ClientId = _currentUser.ClientId,
                TransactionId = transaction.Id,
                CustomerRiskScore = customerRisk,
                TransactionRiskScore = transactionRisk.Score,
                ScreeningRiskScore = screeningRisk,
                OverallRiskScore = overallRisk,
                RiskLevel = riskLevel,
                ModelVersion = ModelVersion,
                Factors = string.Join(" | ", reasons),
                CreatedBy = "AML Engine",
                CalculatedAt = DateTime.UtcNow
            };
            _db.RiskAssessments.Add(assessment);
            await _db.SaveChangesAsync();

            var decision = new AmlDecision
            {
                ClientId = _currentUser.ClientId,
                TransactionId = transaction.Id,
                RiskAssessmentId = assessment.Id,
                CorrelationId = transaction.CorrelationId ?? Guid.NewGuid().ToString("N"),
                Decision = decisionValue,
                Reasons = string.Join(" | ", reasons),
                RuleVersions = transactionRisk.RuleVersions,
                ModelVersion = ModelVersion,
                ListVersions = matches.Count == 0
                    ? "manual-v1"
                    : string.Join(", ", matches.Select(x => x.ListVersion).Distinct()),
                CreatedBy = "AML Engine"
            };
            _db.AmlDecisions.Add(decision);

            transaction.Decision = decisionValue;
            transaction.Status = decisionValue == "ALLOW" ? "CLEARED" : "HELD";
            transaction.RiskScore = overallRisk;
            transaction.RiskLevel = riskLevel;
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
                    ClientId = _currentUser.ClientId,
                    AlertReference = Reference("ALT"),
                    TransactionId = transaction.Id,
                    RiskAssessmentId = assessment.Id,
                    Severity = decisionValue == "BLOCK" ? "CRITICAL" : riskLevel,
                    Reason = decision.Reasons,
                    Status = "NEW",
                    CreatedBy = "AML Engine"
                };
                _db.ComplianceAlerts.Add(alert);
                await _db.SaveChangesAsync();

                complianceCase = new ComplianceCase
                {
                    ClientId = _currentUser.ClientId,
                    CaseReference = Reference("CASE"),
                    AlertId = alert.Id,
                    TransactionId = transaction.Id,
                    Status = "NEW",
                    CreatedBy = "AML Engine"
                };
                _db.ComplianceCases.Add(complianceCase);
            }

            AddAudit("Evaluate", "AmlDecision", transaction.Id,
                $"{transaction.TransactionReference}: {decisionValue}; score {overallRisk}; correlation {decision.CorrelationId}.");
            await _db.SaveChangesAsync();
            await databaseTransaction.CommitAsync();

            return new AmlEvaluationResult
            {
                Transaction = transaction,
                RiskAssessment = assessment,
                Decision = decision,
                Matches = matches,
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
            .Where(x => x.ClientId == _currentUser.ClientId)
            .OrderByDescending(x => x.CreatedDate)
            .Take(500)
            .ToListAsync();

    public Task<ComplianceAlert?> GetAlertAsync(int id) =>
        _db.ComplianceAlerts.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && x.ClientId == _currentUser.ClientId);

    public async Task<bool> UpdateAlertAsync(int id, string status, string? resolution)
    {
        var normalizedStatus = status.Trim().ToUpperInvariant();
        if (normalizedStatus is "RESOLVED" or "CLOSED" && string.IsNullOrWhiteSpace(resolution))
            throw new InvalidOperationException("A resolution is required before an alert can be closed.");
        var alert = await _db.ComplianceAlerts.FirstOrDefaultAsync(x =>
            x.Id == id && x.ClientId == _currentUser.ClientId);
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
            .Where(x => x.ClientId == _currentUser.ClientId)
            .OrderByDescending(x => x.CreatedDate)
            .Take(500)
            .ToListAsync();

    public Task<ComplianceCase?> GetCaseAsync(int id) =>
        _db.ComplianceCases.AsNoTracking().FirstOrDefaultAsync(x =>
            x.Id == id && x.ClientId == _currentUser.ClientId);

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
            x.Id == id && x.ClientId == _currentUser.ClientId);
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
        var records = await _db.AmlRecords.AsNoTracking()
            .Where(x => x.ClientId == _currentUser.ClientId && x.IsActive)
            .ToListAsync();
        var subjects = new[] { transaction.SenderName, transaction.ReceiverName };
        var matches = new List<ScreeningMatch>();

        foreach (var subject in subjects.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
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
                    ClientId = _currentUser.ClientId,
                    TransactionId = transaction.Id,
                    AmlRecordId = record.Id,
                    SubjectName = subject,
                    MatchedName = record.FullName,
                    MatchScore = score,
                    MatchStatus = score >= 95 ? "CONFIRMED_MATCH" : "POTENTIAL_MATCH",
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
            .Where(x => x.ClientId == _currentUser.ClientId && x.IsActive &&
                x.ApprovalStatus == "Active" && x.EffectiveFrom <= now &&
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
                    x.ClientId == _currentUser.ClientId &&
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

        if (transaction.Amount >= 1_000_000m)
        {
            score = Math.Max(score, 60);
            reasons.Add("Large transaction amount increased transaction risk.");
        }
        else if (transaction.Amount >= 500_000m)
        {
            score = Math.Max(score, 35);
            reasons.Add("Elevated transaction amount increased transaction risk.");
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

    private static string Decide(string screeningStatus, int score, string ruleAction)
    {
        if (screeningStatus == "CONFIRMED_MATCH" ||
            ruleAction.Equals("Block", StringComparison.OrdinalIgnoreCase))
            return "BLOCK";
        if (screeningStatus == "POTENTIAL_MATCH" || score >= 60 ||
            ruleAction.Equals("Review", StringComparison.OrdinalIgnoreCase) ||
            ruleAction.Equals("Hold", StringComparison.OrdinalIgnoreCase) ||
            ruleAction.Equals("Alert", StringComparison.OrdinalIgnoreCase))
            return "REVIEW";
        return "ALLOW";
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

    private static decimal Similarity(string first, string second)
    {
        var left = Normalize(first);
        var right = Normalize(second);
        if (left.Length == 0 || right.Length == 0) return 0;
        if (left == right) return 100;

        var distance = Levenshtein(left, right);
        return Math.Round((1m - distance / (decimal)Math.Max(left.Length, right.Length)) * 100m, 2);
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;
        foreach (var character in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }
        return builder.ToString().Trim();
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
            ClientId = _currentUser.ClientId,
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

    private sealed record TransactionRiskResult(int Score, string HighestAction, string RuleVersions);
}

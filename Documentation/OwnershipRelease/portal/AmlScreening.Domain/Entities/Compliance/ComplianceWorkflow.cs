using AmlScreening.Domain.Common;

namespace AmlScreening.Domain.Entities.Compliance;

public sealed class Customer : BaseEntity, IUserOwnedEntity
{
    public int ClientId { get; set; }
    public long? OwnerUserId { get; set; }
    public string CustomerReference { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string Occupation { get; set; } = string.Empty;
    public string SourceOfFunds { get; set; } = string.Empty;
    public bool IsPep { get; set; }
    public int RiskScore { get; set; }
    public string RiskLevel { get; set; } = "LOW";
}

public sealed class AmlTransaction : BaseEntity, IUserOwnedEntity
{
    public int ClientId { get; set; }
    public long? OwnerUserId { get; set; }
    public string TransactionReference { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NPR";
    public string OriginCountry { get; set; } = string.Empty;
    public string DestinationCountry { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "NEW";
    public string? CorrelationId { get; set; }
    public string? Decision { get; set; }
    public int? RiskScore { get; set; }
    public string? RiskLevel { get; set; }
    public string? ScreeningStatus { get; set; }
    public string? DecisionReasons { get; set; }
    public DateTime? EvaluatedDate { get; set; }

    public string PurposeCode { get; set; } = string.Empty;
    public int RegisteredReceiverCount { get; set; }
    public string RelationshipCode { get; set; } = string.Empty;
}

public sealed class ScreeningMatch : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }
    public int TransactionId { get; set; }
    public int AmlRecordId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string MatchedName { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
    public string MatchStatus { get; set; } = "POTENTIAL_MATCH";
    public string SourceName { get; set; } = string.Empty;
    public string ListVersion { get; set; } = "manual-v1";
    public string ComparedFields { get; set; } = "Name";
}

public sealed class RiskAssessment : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }
    public int TransactionId { get; set; }
    public int CustomerRiskScore { get; set; }
    public int TransactionRiskScore { get; set; }
    public int ScreeningRiskScore { get; set; }
    public int OverallRiskScore { get; set; }
    public string RiskLevel { get; set; } = "LOW";
    public string ModelVersion { get; set; } = "risk-v1.0";
    public string Factors { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AmlDecision : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }
    public int TransactionId { get; set; }
    public int RiskAssessmentId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Decision { get; set; } = "ALLOW";
    public string Reasons { get; set; } = string.Empty;
    public string RuleVersions { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = "risk-v1.0";
    public string ListVersions { get; set; } = "manual-v1";
}

public sealed class ComplianceAlert : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }
    public string AlertReference { get; set; } = string.Empty;
    public int TransactionId { get; set; }
    public int RiskAssessmentId { get; set; }
    public string Severity { get; set; } = "MEDIUM";
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "NEW";
    public long? AssignedUserId { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? Resolution { get; set; }
}

public sealed class ComplianceCase : BaseEntity, IClientOwnedEntity
{
    public int ClientId { get; set; }
    public string CaseReference { get; set; } = string.Empty;
    public int AlertId { get; set; }
    public int TransactionId { get; set; }
    public string Status { get; set; } = "NEW";
    public long? AssignedUserId { get; set; }
    public string? Disposition { get; set; }
    public string? Notes { get; set; }
    public DateTime? ClosedDate { get; set; }
}

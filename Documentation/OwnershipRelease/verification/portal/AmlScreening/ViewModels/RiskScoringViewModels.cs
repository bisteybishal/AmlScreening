using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using AmlScreening.Domain.Entities.Compliance;

namespace AmlScreening.Portal.ViewModels;

public sealed class RiskScoringPageViewModel
{
    public static RiskScoringPageViewModel From(AmlScreening.Application.Models.RiskScoringSetupResult result) => new()
    {
        ActiveModel = result.ActiveModel, Factors = result.Factors, SelectedFactor = result.SelectedFactor,
        Rules = result.Rules, Bands = result.Bands, PageNumber = result.PageNumber,
        PageSize = result.PageSize, TotalCount = result.TotalCount,
        HasRuleDraft = result.HasRuleDraft, HasBandDraft = result.HasBandDraft,
        CanEditRules = result.CanEditRules, CanEditBands = result.CanEditBands
    };
    public bool CanEditRules { get; init; }
    public bool CanEditBands { get; init; }
    public bool HasRuleDraft { get; init; }
    public bool HasBandDraft { get; init; }
    public RiskModel? ActiveModel { get; init; }
    public IReadOnlyList<RiskFactor> Factors { get; init; } = Array.Empty<RiskFactor>();
    public RiskFactor? SelectedFactor { get; init; }
    public IReadOnlyList<RiskFactorRule> Rules { get; init; } = Array.Empty<RiskFactorRule>();
    public IReadOnlyList<RiskBand> Bands { get; init; } = Array.Empty<RiskBand>();
    public string Search { get; init; } = string.Empty;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public int FirstRecord => TotalCount == 0 ? 0 : (PageNumber - 1) * PageSize + 1;
    public int LastRecord => Math.Min(PageNumber * PageSize, TotalCount);
}

public sealed class RiskRuleEditViewModel
{
    [ValidateNever] public string ValueType { get; set; } = "";
    [StringLength(100)] public string? ValueCode { get; set; }
    [StringLength(200)] public string? ValueName { get; set; }
    public decimal? MinimumValue { get; set; }
    public decimal? MaximumValue { get; set; }
    public int Id { get; set; }
    public string FactorCode { get; set; } = string.Empty;
    [ValidateNever] public string ValueDescription { get; set; } = string.Empty;
    [Range(0, 100)] public int Score { get; set; }
    [StringLength(1000)] public string? Remarks { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class RiskBandEditViewModel
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string RiskLevel { get; set; } = string.Empty;
    [Range(0, 100)] public int MinimumScore { get; set; }
    [Range(0, 100)] public int? MaximumScore { get; set; }
    [Required] public string Decision { get; set; } = "ALLOW";
    [StringLength(1000)] public string? Remarks { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class RiskScoreSetupEditViewModel
{
    [MinLength(1, ErrorMessage = "At least one risk range is required.")]
    public List<RiskBandEditViewModel> Bands { get; set; } = [];
}

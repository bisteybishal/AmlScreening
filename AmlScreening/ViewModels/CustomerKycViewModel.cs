using AmlScreening.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public class CustomerKycViewModel : IValidatableObject
{
    [Required]
    [StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;


    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateTime? DateOfBirth { get; set; }


    [Required]
    [StringLength(100)]
    public string Country { get; set; } = string.Empty;


    [Required]
    [StringLength(20)]
    public string Gender { get; set; } = string.Empty;


    [Required]
    [StringLength(100)]
    [Display(Name = "Screening type")]
    public string ScreeningType { get; set; } = string.Empty;


    [Required]
    [StringLength(150)]
    public string Occupation { get; set; } = string.Empty;


    [Required]
    [StringLength(150)]
    [Display(Name = "Source of funds")]
    public string SourceOfFunds { get; set; } = string.Empty;


    public List<SelectListItem> CountryOptions { get; set; } = [];

    public List<SelectListItem> GenderOptions { get; set; } = [];

    public List<SelectListItem> ScreeningTypeOptions { get; set; } = [];

    public List<SelectListItem> OccupationOptions { get; set; } = [];

    public List<SelectListItem> SourceOfFundsOptions { get; set; } = [];


    public CustomerKycResult? Result { get; set; }


    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (DateOfBirth.HasValue &&
            DateOfBirth.Value.Date > DateTime.UtcNow.Date)
        {
            yield return new ValidationResult(
                "Date of birth cannot be in the future.",
                new[] { nameof(DateOfBirth) });
        }
    }
}


public class CustomerKycResult
{
    public List<AmlRecord> Matches { get; set; } = [];

    public List<string> Sources { get; set; } = [];

    public List<KycMatchEvidence> MatchEvidence { get; set; } = [];


    public decimal RiskScore { get; set; }

    public string RiskLevel { get; set; } = string.Empty;

    public string? ModelVersion { get; set; }


    public bool IsProvisional =>
        Matches.Count > 0;


    public string ScreeningStatus =>
        IsProvisional
            ? "REVIEW_REQUIRED"
            : "NO_CANDIDATE_FOUND";


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


public class KycMatchEvidence
{
    public int AmlRecordId { get; set; }


    public string MatchedName { get; set; }
        = string.Empty;

    public string SourceName { get; set; }
        = string.Empty;

    public string ReferenceNo { get; set; }
        = string.Empty;


    public decimal NameMatchPercent { get; set; }


    public bool DateOfBirthMatched { get; set; }

    public bool CountryMatched { get; set; }

    public bool GenderMatched { get; set; }


    public decimal MatchPercent { get; set; }

    public string MatchStatus { get; set; }
        = string.Empty;
}
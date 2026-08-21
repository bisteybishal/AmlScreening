using AmlScreening.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class AmlRecordsPageViewModel
{
    public AmlRecordInputModel Input { get; set; } = new();
    public IReadOnlyList<AmlRecord> Records { get; set; } = Array.Empty<AmlRecord>();
    public string Search { get; set; } = string.Empty;
    public string RecordType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int ActiveRecords { get; set; }
    public int IndividualRecords { get; set; }
    public int EntityRecords { get; set; }
    public int CountriesCovered { get; set; }
}

public sealed class AmlRecordInputModel
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "Record type")]
    public string RecordType { get; set; } = "Individual";

    [Required, StringLength(200)]
    [Display(Name = "Full name / Entity name")]
    public string FullName { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Date of birth / Incorporation")]
    public DateTime? DateOfBirth { get; set; }

    [Required, StringLength(100)]
    public string Country { get; set; } = string.Empty;

    [StringLength(100)]
    public string Nationality { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Reference number")]
    public string ReferenceNo { get; set; } = string.Empty;

    [Required, StringLength(150)]
    [Display(Name = "Source name")]
    public string SourceName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string Remarks { get; set; } = string.Empty;

    [Display(Name = "Active record")]
    public bool IsActive { get; set; } = true;
}

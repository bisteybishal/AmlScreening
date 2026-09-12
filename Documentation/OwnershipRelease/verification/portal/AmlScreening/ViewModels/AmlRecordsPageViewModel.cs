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
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int FilteredRecords { get; set; }
    public int TotalPages { get; set; } = 1;
    public int FirstRecordNumber => FilteredRecords == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastRecordNumber => Math.Min(Page * PageSize, FilteredRecords);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public IReadOnlyList<string> RecordTypes { get; set; } = AmlRecordOptions.RecordTypes;
}

public sealed class AmlRecordInputModel
{
    public int Id { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

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
    public IReadOnlyList<string> RecordTypes { get; set; } = AmlRecordOptions.RecordTypes;
    public IReadOnlyList<string> Countries { get; set; } = AmlRecordOptions.Countries;
    public IReadOnlyList<string> Nationalities { get; set; } = AmlRecordOptions.Nationalities;
    public IReadOnlyList<string> Sources { get; set; } = AmlRecordOptions.Sources;
}

public static class AmlRecordOptions
{
    public static readonly IReadOnlyList<string> RecordTypes =
        new[] { "Individual", "Entity", "Vessel" };

    public static readonly IReadOnlyList<string> Countries =
        new[] { "Nepal", "India", "China", "United States", "United Kingdom",
            "United Arab Emirates", "Jordan", "Panama" };

    public static readonly IReadOnlyList<string> Nationalities =
        new[] { "Nepali", "Indian", "Chinese", "American", "British",
            "Jordanian", "Panamanian" };

    public static readonly IReadOnlyList<string> Sources =
        new[] { "PEP Local", "PEP International", "EU Sanctions", "UN Sanctions",
            "CIB", "Other List" };
}

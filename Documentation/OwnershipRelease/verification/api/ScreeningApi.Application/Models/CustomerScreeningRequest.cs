using System.ComponentModel.DataAnnotations;

namespace ScreeningApi.Application.Models;

public sealed class CustomerScreeningRequest
{
    [Required, StringLength(100)]
    public string ClientReference { get; set; } = string.Empty;

    [Required]
    public CustomerDetails Customer { get; set; } = new();
}

public sealed class CustomerDetails
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    [RegularExpression("^[A-Z]{3}$")]
    public string? Nationality { get; set; }

    [RegularExpression("^[A-Z]{3}$")]
    public string? Country { get; set; }

    [StringLength(100)]
    public string? Occupation { get; set; }

    [StringLength(200)]
    public string? SourceOfFunds { get; set; }

    [StringLength(50)]
    public string? IdentificationType { get; set; }

    [StringLength(100)]
    public string? IdentificationNumber { get; set; }
}
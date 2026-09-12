using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class ScreeningBatchRequest : IValidatableObject
{
    public List<string?> Names { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Names is null || Names.Count is < 1 or > 3)
        {
            yield return new ValidationResult("Enter between one and three names.", new[] { nameof(Names) });
            yield break;
        }
        if (Names.All(string.IsNullOrWhiteSpace))
            yield return new ValidationResult("Enter at least one name.", new[] { nameof(Names) });
        if (Names.Any(name => name is { Length: > 200 }))
            yield return new ValidationResult("Each name must contain at most 200 characters.", new[] { nameof(Names) });
    }
}

public sealed class ScreeningBatchResultsViewModel
{
    public List<ScreeningSearchResultsViewModel> Groups { get; set; } = new();
}

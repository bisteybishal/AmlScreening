using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class ScreeningPageViewModel
{
    [Required(
        ErrorMessage = "Enter a name to search.")]

    [StringLength(
        200,
        MinimumLength = 2,
        ErrorMessage =
            "Enter between 2 and 200 characters.")]

    public string Name { get; set; }
        = string.Empty;
}
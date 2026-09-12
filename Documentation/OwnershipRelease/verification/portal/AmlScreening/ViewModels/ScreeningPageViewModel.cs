using System.ComponentModel.DataAnnotations;
using AmlScreening.Domain.Entities;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AmlScreening.Portal.ViewModels;

public sealed class ScreeningPageViewModel
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = "";
    public int Page { get; set; } = 1;
    [BindNever]
    public bool HasSearched { get; set; }
    [BindNever]
    public PagedResult<AmlRecord> Results { get; set; } = new() { PageNumber = 1, PageSize = 10 };
}

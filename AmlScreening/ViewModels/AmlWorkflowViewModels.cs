using AmlScreening.Application.Models;
using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class TransactionDetailsViewModel
{
    public required AmlTransactionDetails Details { get; init; }
}

public sealed class AlertUpdateModel
{
    public int Id { get; set; }
    [Required] public string Status { get; set; } = "INVESTIGATING";
    [StringLength(2000)] public string? Resolution { get; set; }
}

public sealed class CaseUpdateModel
{
    public int Id { get; set; }
    [Required] public string Status { get; set; } = "INVESTIGATION";
    [StringLength(100)] public string? Disposition { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }
}

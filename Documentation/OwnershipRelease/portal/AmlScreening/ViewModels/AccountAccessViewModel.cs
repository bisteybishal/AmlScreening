using AmlScreening.Application.Models;
using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class AccountAccessViewModel
{
    public AccountAccessInfo Account { get; set; } = new();
    public IReadOnlyList<ApiApplicationInfo> Applications { get; set; } = [];
}

public sealed class LinkApiApplicationRequest
{
    [Range(1, long.MaxValue)] public long UserId { get; set; }
    [Required, StringLength(300)] public string Issuer { get; set; } = "";
    [Required, StringLength(300)] public string Subject { get; set; } = "";
    public bool IdentityVerified { get; set; }
}

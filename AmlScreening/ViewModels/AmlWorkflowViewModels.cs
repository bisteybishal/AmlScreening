using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities.Compliance;
using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels;

public sealed class CustomerInputModel
{
    public int Id { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [Required, StringLength(100)]
    [Display(Name = "Customer reference")]
    public string CustomerReference { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Date of birth")]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(100)] public string Country { get; set; } = string.Empty;
    [StringLength(100)] public string Nationality { get; set; } = string.Empty;
    [StringLength(150)] public string Occupation { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Source of funds")]
    public string SourceOfFunds { get; set; } = string.Empty;

    [Display(Name = "Politically exposed person (PEP)")]
    public bool IsPep { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class TransactionInputModel
{
    [Required, StringLength(100)]
    [Display(Name = "Transaction reference")]
    public string TransactionReference { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Select a customer.")]
    [Display(Name = "Customer")]
    public int CustomerId { get; set; }

    [Required, StringLength(200)]
    [Display(Name = "Sender name")]
    public string SenderName { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Receiver name")]
    public string ReceiverName { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Amount { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "NPR";

    [Required, StringLength(100)]
    [Display(Name = "Origin country")]
    public string OriginCountry { get; set; } = "Nepal";

    [Required, StringLength(100)]
    [Display(Name = "Destination country")]
    public string DestinationCountry { get; set; } = string.Empty;

    [Required, StringLength(50)] public string Channel { get; set; } = "Branch";

    [Display(Name = "Transaction date")]
    public DateTime TransactionDate { get; set; } = DateTime.Now;

    [Display(Name = "Evaluate immediately")]
    public bool EvaluateImmediately { get; set; } = true;

    public IReadOnlyList<Customer> Customers { get; set; } = Array.Empty<Customer>();
}

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

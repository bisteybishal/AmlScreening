using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels
{
    public sealed class SubscriptionAddModel
    {
        public int PlanId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
       
        [Required, StringLength(50)]
        [Display(Name = "Source Code")]
        public string SourceCode { get; set; } = string.Empty;

        [Required, StringLength(200)]
        [Display(Name = "SourceName")]
        public string SourceName { get; set; } = string.Empty;

        [Range(1, 120)]
        [Display(Name = "Duration in Months")]
        public int DurationMonths { get; set; }

        [Range(typeof(decimal), "0.01", "999999999")]
        [Display(Name = "Subscription rate")]
        public decimal Rate { get; set; }

        [Display(Name = "Active subscription")]
        public bool IsActive { get; set; } = true;
    }

    public sealed class SubscriptionPageViewModel
    {
        public IReadOnlyList<AmlScreening.Domain.Entities.SubscriptionPlan> Plans { get; set; } =
            Array.Empty<AmlScreening.Domain.Entities.SubscriptionPlan>();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 6;
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; } = 1;
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
        public string? ErrorMessage { get; set; }
    }

}

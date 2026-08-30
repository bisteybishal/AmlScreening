using System.ComponentModel.DataAnnotations;

namespace AmlScreening.Portal.ViewModels
{
    public sealed class SubscriptionAddModel
    {
        public int PlanId { get; set; }
       
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

}

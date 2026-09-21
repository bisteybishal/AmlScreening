using AmlScreening.Application.Models;

namespace AmlScreening.Portal.ViewModels
{
    public sealed class PartnerBranchesViewModel
    {
        public int PartnerId { get; set; }

        public string PartnerCode { get; set; }
            = string.Empty;

        public string PartnerName { get; set; }
            = string.Empty;

        public bool IsActive { get; set; }

        public IReadOnlyList<BranchListItem>
            Branches
        { get; set; }
            = Array.Empty<BranchListItem>();
    }
}

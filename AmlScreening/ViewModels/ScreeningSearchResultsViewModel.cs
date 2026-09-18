using AmlScreening.Domain.Entities;

namespace AmlScreening.Portal.ViewModels
{
    public sealed class ScreeningSearchResultsViewModel
    {
        public string SearchName { get; set; } = "";
        public IReadOnlyList<AmlRecord> Results { get; set; } = Array.Empty<AmlRecord>();
        public bool HasMore { get; set; }
    }
}

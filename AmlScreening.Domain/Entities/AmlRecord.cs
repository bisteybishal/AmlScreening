using AmlScreening.Domain.Common;
using System.ComponentModel.DataAnnotations.Schema;

namespace AmlScreening.Domain.Entities
{
    public class AmlRecord : BaseEntity
    {
        public int PartnerId { get; set; }

        public int BranchId { get; set; }

        public long? CreatedByUserId { get; set; }
        public string RecordType { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public DateTime? DateOfBirth { get; set; }

        public string? Country { get; set; }

        public string? Nationality { get; set; }

        public string ReferenceNo { get; set; } = string.Empty;

        public string SourceName { get; set; } = string.Empty;

        public string? Remarks { get; set; }
        public byte[]? Photo { get; set; }

        public string? PhotoContentType { get; set; }
        [NotMapped]
        public bool HasPhoto { get; set; }
        public string? NormalizedName { get; set; }

        public string? NamePrefix { get; set; }

        public DateTime? LastSyncDate { get; set; }

        public DateTime? SourceUpdatedDate { get; set; }
        public bool IsGlobal { get; set; }

        public string? Aliases { get; set; }

        public string? Occupation { get; set; }

        public string? Position { get; set; }

        public string? PoliticalParties { get; set; }

        public string? SocialMediaLinks { get; set; }

        public string? Description { get; set; }
        public string? Gender { get; set; }

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

    }
   
}

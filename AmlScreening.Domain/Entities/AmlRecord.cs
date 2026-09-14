using AmlScreening.Domain.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace AmlScreening.Domain.Entities
{
    public class AmlRecord : BaseEntity, IUserOwnedEntity
    {
        public int ClientId { get; set; }

        public long OwnerUserId { get; set; }

        public bool IsShared { get; set; }

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

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }

        public int TotalPages =>
            (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}

using AmlScreening.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Domain.Entities
{
    public class AmlRecord:BaseEntity
    {
        public string RecordType { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public DateTime? DateOfBirth { get; set; }

        public string Country { get; set; } = string.Empty;

        public string Nationality { get; set; } = string.Empty;

        public string ReferenceNo { get; set; } = string.Empty;

        public string SourceName { get; set; } = string.Empty;

        public string Remarks { get; set; } = string.Empty;

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

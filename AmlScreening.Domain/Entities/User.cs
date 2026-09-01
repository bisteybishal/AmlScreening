using AmlScreening.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Domain.Entities
{
    public class User
    {
        public long Id { get; set; }

        public int ClientId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

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

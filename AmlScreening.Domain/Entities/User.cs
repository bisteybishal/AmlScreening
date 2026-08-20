using AmlScreening.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Domain.Entities
{
    public class User:BaseEntity
    {
        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

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

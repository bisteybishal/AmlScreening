using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Models
{
    public class LoginResult
    {
        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;

        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
    }
}

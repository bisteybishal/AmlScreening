using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Models;
    public class ErrorLogModel
    {
        public string ErrorDescription { get; set; } = string.Empty;
        public string? PartnerCode { get; set; }
        public string? ErrorNumber { get; set; }
        public string Script { get; set; } = string.Empty;
        public string ErrorScript { get; set; } = string.Empty;
        public string? QueryString { get; set; }
        public string? IpAddress { get; set; }
    }

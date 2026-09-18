using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Models;

public sealed class ErrorLogRequest
{
    public string ErrorDesc { get; set; }
        = string.Empty;

    public string? PartnerCode { get; set; }

    public string? ErrorNumber { get; set; }

    public string? Script { get; set; }

    public string? ErrorScript { get; set; }

    public string? QueryString { get; set; }

    public string? IpAddress { get; set; }

    public long? UserId { get; set; }

    public string? Username { get; set; }
}

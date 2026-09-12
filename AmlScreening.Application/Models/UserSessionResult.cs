using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Models;

public sealed class UserSessionResult
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; } 
}

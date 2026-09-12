using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Models;

public sealed class ClientOption
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
}

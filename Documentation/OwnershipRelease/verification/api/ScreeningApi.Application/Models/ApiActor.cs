namespace ScreeningApi.Application.Models;

// Resolved from a VALIDATED access token through dbo.ApiApplications.
// These values never come from the customer's request body.
public sealed class ApiActor
{
    public int ApiApplicationId { get; set; }
    public long UserId { get; set; }
    public int ClientId { get; set; }
    public bool CanManageCustomers { get; set; }
    public bool CanScreen { get; set; }
    public bool CanViewCustomers { get; set; }
    public bool IsSystemAdministrator { get; set; }
}


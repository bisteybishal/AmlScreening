namespace ScreeningApi.Application.Models;

public sealed class CustomerScreeningDbResult
{
    public bool IsUnMatch { get; set; }
    public bool IsEuMatch { get; set; }
    public bool IsPep { get; set; }
    public bool IsPrivateMatch { get; set; }
    public bool IsOtherSourceMatch { get; set; }
    public int HighestMatchScore { get; set; }
}


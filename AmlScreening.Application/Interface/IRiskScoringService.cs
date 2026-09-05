using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities.Compliance;

namespace AmlScreening.Application.Interface;

public interface IRiskScoringService
{
    Task SaveBandRangesAsync(IReadOnlyList<RiskBandInput> ranges, CancellationToken cancellationToken = default);
    Task SaveRuleAsync(RiskRuleInput input, CancellationToken cancellationToken = default);
    Task DeleteRuleAsync(int id, byte[] rowVersion, CancellationToken cancellationToken = default);
    Task ActivateRulesAsync(string factorCode, CancellationToken cancellationToken = default);
    Task SaveBandAsync(RiskBandInput input, CancellationToken cancellationToken = default);
    Task DeleteBandAsync(int id, byte[] rowVersion, CancellationToken cancellationToken = default);
    Task ActivateBandsAsync(CancellationToken cancellationToken = default);
    Task<RiskScoringResult> CalculateAsync(
        RiskScoringInput input,
        CancellationToken cancellationToken = default);

    Task<RiskScoringSetupResult> GetSetupAsync(
        string? factorCode,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<RiskFactorRule?> GetRuleAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<RiskBand?> GetBandAsync(
        int id,
        CancellationToken cancellationToken = default);

}

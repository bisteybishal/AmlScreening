using ScreeningApi.Application.Models;

namespace ScreeningApi.Application.Services;

public sealed class CustomerKycRiskCalculator
{
    public CustomerKycResponse Calculate(CustomerKycRequest request, CustomerScreeningDbResult aml,
        IReadOnlyList<KycRiskRule> rules, IReadOnlyList<KycRiskBand> bands)
    {
        var groups = rules.GroupBy(r => r.FactorId).ToList();
        if (groups.Count == 0 || groups.Sum(g => g.First().WeightPercent) != 100m)
            throw new KycConfigurationException("Active customer factor weights must total 100%.");
        if (groups.Select(g => g.First().FactorCode.ToUpperInvariant()).Distinct().Count() != groups.Count)
            throw new KycConfigurationException("Customer factor codes must be unique.");

        var result = new CustomerKycResponse
        {
            CustomerReference = request.CustomerReference.Trim(),
            CalculatedAtUtc = DateTime.UtcNow
        };
        foreach (var group in groups)
        {
            var factor = group.First();
            if (factor.WeightPercent < 0 || factor.WeightPercent > 100)
                throw new KycConfigurationException("Invalid customer factor weight.");
            var input = factor.FactorCode.ToUpperInvariant() switch
            {
                "OCCUPATION" => request.Customer.Occupation,
                "SOURCE_OF_FUNDS" => request.Customer.SourceOfFunds,
                "CUSTOMER_PEP" => aml.IsPep ? "TRUE" : "FALSE",
                "COUNTRY" => request.Customer.Country,
                "NATIONALITY" => request.Customer.Nationality,
                _ => throw new KycConfigurationException($"Unsupported customer factor: {factor.FactorCode}.")
            };
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException($"A value is required for {factor.FactorCode}.");
            input = input.Trim().ToUpperInvariant();
            string Normalize(string? value) => factor.FactorCode.Equals("CUSTOMER_PEP", StringComparison.OrdinalIgnoreCase)
                ? value?.Trim().ToUpperInvariant() switch { "1" or "YES" => "TRUE", "0" or "NO" => "FALSE", var v => v ?? "" }
                : value?.Trim().ToUpperInvariant() ?? "";
            var candidates = group.Where(r => r.RuleId.HasValue && Normalize(r.ValueCode) == input).ToList();
            if (candidates.Count == 0)
                candidates = group.Where(r => r.RuleId.HasValue && Normalize(r.ValueCode) == "DEFAULT").ToList();
            if (candidates.Count != 1)
                throw new KycConfigurationException($"Expected one effective approved rule for {factor.FactorCode} ({input}).");
            var rule = candidates[0];
            if (rule.Score is < 0 or > 100)
                throw new KycConfigurationException("Risk rule scores must be between 0 and 100.");
            result.Factors.Add(new(factor.FactorCode, input, rule.RuleId!.Value, rule.RuleVersion,
                rule.Score, factor.WeightPercent, rule.Score * factor.WeightPercent / 100m));
        }
        result.RiskScore = (int)Math.Round(result.Factors.Sum(f => f.WeightedScore), MidpointRounding.AwayFromZero);
        var matches = bands.Where(b => result.RiskScore >= b.MinimumScore &&
            (b.MaximumScore == null || result.RiskScore <= b.MaximumScore)).ToList();
        if (matches.Count != 1)
            throw new KycConfigurationException("Risk score must match exactly one approved risk band.");
        var band = matches[0];
        result.RiskLevel = band.RiskLevel;
        result.RiskBandId = band.Id;
        result.BandVersion = band.BandVersion;
        result.Decision = band.Decision.Trim().ToUpperInvariant() switch
        {
            "ALLOW" or "APPROVE" => "ALLOW",
            "REVIEW" => "REVIEW",
            "BLOCK" or "REJECT" => "BLOCK",
            _ => throw new KycConfigurationException("Unsupported risk band decision.")
        };
        // A positive score also catches matching AML sources not represented by the three flags.
        var hasMatch = aml.IsUnMatch || aml.IsEuMatch || aml.IsPep || aml.HighestMatchScore > 0;
        if (hasMatch && result.Decision == "ALLOW") result.Decision = "REVIEW";
        var reasons = new List<string>();
        if (aml.IsUnMatch) reasons.Add("Potential UN exact-name match found.");
        if (aml.IsEuMatch) reasons.Add("Potential EU exact-name match found.");
        if (aml.IsPep) reasons.Add("Potential PEP exact-name match found.");
        if (aml.IsPrivateMatch) reasons.Add("Potential exact-name match on your private blocked list.");
        if (aml.IsOtherSourceMatch) reasons.Add("Potential exact-name match on an authorized source.");
        reasons.Add($"Approved risk band: {band.RiskLevel}; decision: {result.Decision}.");
        if (!hasMatch) reasons.Add("No exact-name match found within your permitted records.");
        if (reasons.Count == 0) reasons.Add(hasMatch ? "Potential AML name match found." : "No exact-name match found.");
        result.AmlResult = new CustomerScreeningResponse
        {
            ClientReference = result.CustomerReference,
            ScreeningStatus = hasMatch ? "POTENTIAL_MATCH" : "CLEAR",
            IsUnMatch = aml.IsUnMatch, IsEuMatch = aml.IsEuMatch, IsPep = aml.IsPep,
            HighestMatchScore = aml.HighestMatchScore,
            CustomerRiskScore = result.RiskScore, RiskLevel = result.RiskLevel,
            Decision = result.Decision, Reasons = reasons,
            CorrelationId = Guid.NewGuid().ToString("N")
        };
        return result;
    }
}

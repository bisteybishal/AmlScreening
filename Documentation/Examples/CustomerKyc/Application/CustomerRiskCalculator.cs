using System.Text;

namespace CustomerKyc.Application;

// Pure calculation: no HTTP calls and no database writes.
public static class CustomerRiskCalculator
{
    public static RiskResult Calculate(CustomerInput customer, bool potentialPep,
        IReadOnlyList<Factor> factors, IReadOnlyList<Rule> rules, IReadOnlyList<Band> bands)
    {
        var inputs = new Dictionary<string, string>
        {
            ["OCCUPATION"] = Normalize(customer.Occupation),
            ["CUSTOMER_PEP"] = potentialPep ? "YES" : "NO",
            ["SOURCE_OF_FUNDS"] = Normalize(customer.SourceOfFunds)
        };
        var details = new List<FactorDetail>();
        var reasons = new List<string>();
        if (factors.Count != inputs.Count || factors.Select(f => f.FactorCode).Distinct().Count() != inputs.Count ||
            factors.Any(f => !inputs.ContainsKey(f.FactorCode)) ||
            factors.Any(f => f.WeightPercent <= 0 || f.WeightPercent > 100) ||
            factors.Sum(f => f.WeightPercent) != 100)
            reasons.Add("Customer factors are incomplete or weights do not total 100.");

        foreach (var factor in factors)
        {
            var input = inputs.GetValueOrDefault(factor.FactorCode, "UNKNOWN");
            var available = rules.Where(r => r.RiskFactorId == factor.Id).ToList();
            var matches = available.Where(r => Normalize(r.ValueCode) == input).ToList();
            if (matches.Count == 0)
                matches = available.Where(r => Normalize(r.ValueCode) == "DEFAULT").ToList();
            var rule = matches.Count == 1 && matches[0].Score is >= 0 and <= 100 ? matches[0] : null;
            if (rule == null) reasons.Add($"No unique approved rule/default for {factor.FactorCode}.");
            details.Add(new(factor.FactorCode, input, rule?.Id, rule?.RuleVersion, rule?.Score,
                factor.WeightPercent, rule == null ? null : rule.Score * factor.WeightPercent / 100m));
        }
        if (reasons.Count > 0) return new(null, null, "REVIEW", null, null, details, reasons);

        var score = (int)Math.Round(details.Sum(f => f.WeightedScore!.Value), 0, MidpointRounding.AwayFromZero);
        var version = bands.Count == 0 ? 0 : bands.Max(b => b.BandVersion);
        var activeBands = bands.Where(b => b.BandVersion == version).OrderBy(b => b.MinimumScore).ToList();
        var next = 0;
        foreach (var band in activeBands)
        {
            var max = band.MaximumScore ?? 100;
            if (band.MinimumScore != next || max < next || max > 100 ||
                band.Decision.ToUpperInvariant() is not ("ALLOW" or "REVIEW" or "BLOCK"))
                return new(score, null, "REVIEW", null, version, details, ["Approved risk bands have gaps, overlaps or invalid decisions."]);
            next = max + 1;
        }
        if (next != 101)
            return new(score, null, "REVIEW", null, version, details, ["Approved risk bands must cover 0–100."]);
        var selected = activeBands.Single(b => score >= b.MinimumScore && score <= (b.MaximumScore ?? 100));
        return new(score, selected.RiskLevel.ToUpperInvariant(), selected.Decision.ToUpperInvariant(),
            selected.Id, version, details, ["Customer risk calculated from approved client configuration."]);
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "UNKNOWN";
        var text = new StringBuilder();
        bool separator = false;
        foreach (var c in value.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(c)) { text.Append(c); separator = false; }
            else if (!separator) { text.Append('_'); separator = true; }
        }
        return text.ToString().Trim('_');
    }
}

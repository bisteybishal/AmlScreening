using AmlScreening.Application.Interface.Provider;
using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Infrastructure.Provider;

public sealed class AmlSourceStrategyFactory : IAmlSourceStrategyFactory
{
    private readonly IEnumerable<IAmlSourceStrategy> _strategies;

    public AmlSourceStrategyFactory(IEnumerable<IAmlSourceStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IAmlSourceStrategy GetStrategy(string sourceCode)
    {
        var strategy = _strategies.FirstOrDefault(s =>
            string.Equals(s.SourceCode, sourceCode, StringComparison.OrdinalIgnoreCase));

        if (strategy is null)
        {
            throw new NotSupportedException($"No AML source strategy registered for source code '{sourceCode}'.");
        }

        return strategy;
    }
}

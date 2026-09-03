using System;
using System.Collections.Generic;
using System.Text;

namespace AmlScreening.Application.Interface.Provider;

public interface IAmlSourceStrategyFactory
{
    IAmlSourceStrategy GetStrategy(string sourceCode);
}


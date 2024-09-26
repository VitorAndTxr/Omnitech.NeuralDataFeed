using Omnitech.NeuralDataFeed.Domain.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Omnitech.NeuralDataFeed.Provider.Interfaces
{
    public interface ITradingPairProvider
    {
        List<TradingPairSettings>  GetTradingPairs();
    }
}

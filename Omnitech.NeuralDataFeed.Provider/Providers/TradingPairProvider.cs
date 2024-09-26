using Microsoft.Extensions.Options;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Provider.Interfaces;

namespace Omnitech.NeuralDataFeed.Provider.Providers
{
    public class TradingPairProvider : ITradingPairProvider
    {
        private readonly List<TradingPairSettings> _tradingPairSettings;

        public TradingPairProvider(IOptions<List<TradingPairSettings>> options)
        {
            _tradingPairSettings = options.Value;
        }

        public List<TradingPairSettings> GetTradingPairs()
        {
            return _tradingPairSettings;
        }
    }
}

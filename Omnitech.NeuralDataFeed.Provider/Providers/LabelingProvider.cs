using Microsoft.Extensions.Options;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Provider.Interfaces;

namespace Omnitech.NeuralDataFeed.Provider.Providers
{
    public class LabelingProvider : ILabelingProvider
    {
        private readonly LabelingSettings _settings;

        public LabelingProvider(IOptions<LabelingSettings> options)
        {
            _settings = options.Value;
        }

        public LabelingThreshold GetThreshold(string pairName, string timeframe)
        {
            var overrideEntry = _settings.Overrides.FirstOrDefault(o =>
                string.Equals(o.PairName, pairName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(o.Timeframe, timeframe, StringComparison.OrdinalIgnoreCase));

            if (overrideEntry != null)
                return new LabelingThreshold
                {
                    TargetPercent = overrideEntry.TargetPercent,
                    StopPercent   = overrideEntry.StopPercent,
                    MaxLookaheadCandles = overrideEntry.MaxLookaheadCandles
                };

            return _settings.Default;
        }
    }
}

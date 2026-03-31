using Microsoft.Extensions.Logging;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Provider.Interfaces;
using Omnitech.NeuralDataFeed.Service.Interfaces;

namespace Omnitech.NeuralDataFeed.Service.Services
{
    public class SignalService : ISignalService
    {
        private const int LabelBatchSize = 100_000;

        private readonly ILogger<SignalService> _logger;
        private readonly IMarketDataFeaturesRepository _featuresRepository;
        private readonly ITradingPairProvider _tradingPairProvider;
        private readonly ILabelingProvider _labelingProvider;

        public SignalService(
            ILogger<SignalService> logger,
            IMarketDataFeaturesRepository featuresRepository,
            ITradingPairProvider tradingPairProvider,
            ILabelingProvider labelingProvider)
        {
            _logger = logger;
            _featuresRepository = featuresRepository;
            _tradingPairProvider = tradingPairProvider;
            _labelingProvider = labelingProvider;
        }

        public async Task UpdateLabelsAsync(string pairName)
        {
            try
            {
                _logger.LogInformation("{Pair}: updating signal labels", pairName);
                var tradingPair = _tradingPairProvider.GetTradingPairs()
                    .FirstOrDefault(p => p.Name == pairName);

                if (tradingPair == null) return;

                foreach (var timeframe in tradingPair.Timeframes)
                {
                    await UpdateLabelsForTimeframe(pairName, timeframe);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Pair}: label update failed", pairName);
                throw;
            }
        }

        private async Task UpdateLabelsForTimeframe(string pairName, string timeframe)
        {
            var threshold = _labelingProvider.GetThreshold(pairName, timeframe);
            double target = threshold.TargetPercent / 100.0;
            double stop   = threshold.StopPercent / 100.0;
            int maxLookahead = threshold.MaxLookaheadCandles;

            int processed = 0;

            while (true)
            {
                var unlabeled = await _featuresRepository.GetUnlabeledAsync(pairName, timeframe, LabelBatchSize);
                if (unlabeled.Count == 0) break;

                // Fetch already-labeled candles after the batch end to provide forward-scan context for boundary candles
                var futureContext = await _featuresRepository.GetLabeledAfterAsync(
                    pairName, timeframe, unlabeled[^1].CandleOpenTime, 500);
                var workingSet = unlabeled.Concat(futureContext).ToList();

                // Sliding window: for each candle in unlabeled, scan forward in workingSet until target or stop hit
                for (int i = 0; i < unlabeled.Count; i++)
                {
                    var candle = unlabeled[i];
                    double refPrice = candle.ClosePrice;

                    double maxReached  = refPrice;
                    double minReached  = refPrice;
                    bool? buySignal  = null;
                    bool? sellSignal = null;
                    double? targetPct   = null;
                    double? drawdownPct = null;

                    int scanLimit = maxLookahead > 0
                        ? Math.Min(i + 1 + maxLookahead, workingSet.Count)
                        : workingSet.Count;

                    for (int j = i + 1; j < scanLimit; j++)
                    {
                        var future = workingSet[j];

                        maxReached = Math.Max(maxReached, future.HighPrice);
                        minReached = Math.Min(minReached, future.LowPrice);

                        bool stopHit   = future.LowPrice  < refPrice * (1 - stop);
                        bool targetHit = future.HighPrice > refPrice * (1 + target);

                        if (stopHit && targetHit)
                        {
                            // Both in same candle: whichever direction based on open proximity
                            buySignal  = future.OpenPrice >= refPrice;
                            sellSignal = !buySignal;
                            targetPct   = (maxReached - refPrice) / refPrice * 100.0;
                            drawdownPct = (refPrice - minReached) / refPrice * 100.0;
                            break;
                        }

                        if (stopHit)
                        {
                            buySignal   = false;
                            sellSignal  = true;
                            targetPct   = (maxReached - refPrice) / refPrice * 100.0;
                            drawdownPct = (refPrice - future.LowPrice) / refPrice * 100.0;
                            break;
                        }

                        if (targetHit)
                        {
                            buySignal   = true;
                            sellSignal  = false;
                            targetPct   = (future.HighPrice - refPrice) / refPrice * 100.0;
                            drawdownPct = (refPrice - minReached) / refPrice * 100.0;
                            break;
                        }
                    }

                    candle.BuySignal   = buySignal;
                    candle.SellSignal  = sellSignal;
                    candle.TargetPct   = targetPct;
                    candle.DrawdownPct = drawdownPct;
                }

                // Only update candles that got a label (not null)
                var labeled = unlabeled.Where(c => c.BuySignal.HasValue).ToList();
                if (labeled.Any())
                    await _featuresRepository.UpdateLabelsAsync(labeled);

                processed += labeled.Count;
                _logger.LogInformation("{Pair}/{Tf}: labeled {Count} rows (total {Total})",
                    pairName, timeframe, labeled.Count, processed);

                // If no labels were assigned even with future context, there is genuinely insufficient future data
                if (labeled.Count == 0)
                {
                    _logger.LogWarning("{Pair}/{Tf}: {Count} candles remain unlabeled (insufficient future data)",
                        pairName, timeframe, unlabeled.Count);
                    break;
                }
            }
        }

        // Legacy method kept for backwards compatibility
        public async Task UpdateBuySignalAsync()
        {
            _logger.LogInformation("UpdateBuySignalAsync: delegating to UpdateLabelsAsync for all pairs");
            var tradingPairs = _tradingPairProvider.GetTradingPairs();
            foreach (var pair in tradingPairs)
            {
                await UpdateLabelsAsync(pair.Name);
            }
        }
    }
}
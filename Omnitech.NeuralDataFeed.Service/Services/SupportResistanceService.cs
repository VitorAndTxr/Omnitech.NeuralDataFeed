using Microsoft.Extensions.Logging;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using Skender.Stock.Indicators;

namespace Omnitech.NeuralDataFeed.Service.Services
{
    public class SupportResistanceService : ISupportResistanceService
    {
        private const int MaxActiveLevels = 10;
        private const int StaleAfterCandles = 500;

        private readonly ILogger<SupportResistanceService> _logger;
        private readonly IMarketDataTfRepository _marketDataTfRepository;
        private readonly ISupportResistanceLevelRepository _srRepository;

        public SupportResistanceService(
            ILogger<SupportResistanceService> logger,
            IMarketDataTfRepository marketDataTfRepository,
            ISupportResistanceLevelRepository srRepository)
        {
            _logger = logger;
            _marketDataTfRepository = marketDataTfRepository;
            _srRepository = srRepository;
        }

        public async Task UpdateLevelsAsync(string pairName)
        {
            try
            {
                _logger.LogInformation("{Pair}: updating S/R levels from 1h candles", pairName);

                var candles = await _marketDataTfRepository.GetLastNCandlesAsync(pairName, "1h", 2000);
                if (candles.Count < 40)
                {
                    _logger.LogInformation("{Pair}: insufficient 1h candles for S/R detection", pairName);
                    return;
                }

                // Deactivate levels older than StaleAfterCandles hours
                var cutoff = candles[^1].CandleOpenTime.AddHours(-StaleAfterCandles);
                await _srRepository.DeactivateStaleAsync(pairName, cutoff);

                // Compute ATR(14) for clustering threshold
                var quotes = candles.Select(c => (IQuote)new Quote
                {
                    Date   = c.CandleOpenTime,
                    Open   = (decimal)c.OpenPrice,
                    High   = (decimal)c.HighPrice,
                    Low    = (decimal)c.LowPrice,
                    Close  = (decimal)c.ClosePrice,
                    Volume = (decimal)c.Volume
                }).ToList();

                var atrResults = quotes.GetAtr(14).ToList();
                double avgAtr = atrResults
                    .Where(a => a.Atr.HasValue)
                    .TakeLast(50)
                    .Average(a => (double)a.Atr!.Value);

                double clusterThreshold = 0.3 * avgAtr;

                // Detect fractal pivots with N = 5, 10, 20
                var pivotHighs = new List<(int Index, double Price, DateTime Time)>();
                var pivotLows  = new List<(int Index, double Price, DateTime Time)>();

                foreach (int n in new[] { 5, 10, 20 })
                {
                    for (int i = n; i < candles.Count - n; i++)
                    {
                        double high = candles[i].HighPrice;
                        double low  = candles[i].LowPrice;

                        bool isPivotHigh = true;
                        bool isPivotLow  = true;

                        for (int j = 1; j <= n; j++)
                        {
                            if (candles[i - j].HighPrice >= high || candles[i + j].HighPrice >= high)
                                isPivotHigh = false;
                            if (candles[i - j].LowPrice <= low || candles[i + j].LowPrice <= low)
                                isPivotLow = false;
                        }

                        if (isPivotHigh) pivotHighs.Add((i, high, candles[i].CandleOpenTime));
                        if (isPivotLow)  pivotLows.Add((i, low,  candles[i].CandleOpenTime));
                    }
                }

                var resistanceLevels = ClusterPivots(pivotHighs, clusterThreshold, "resistance", pairName, MaxActiveLevels);
                var supportLevels    = ClusterPivots(pivotLows,  clusterThreshold, "support",    pairName, MaxActiveLevels);

                var allLevels = resistanceLevels.Concat(supportLevels).ToList();

                if (allLevels.Any())
                    await _srRepository.UpsertLevelsAsync(allLevels);

                _logger.LogInformation("{Pair}: S/R update complete — {S} supports, {R} resistances",
                    pairName, supportLevels.Count, resistanceLevels.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Pair}: S/R level update failed", pairName);
                throw;
            }
        }

        private static List<SupportResistanceLevel> ClusterPivots(
            List<(int Index, double Price, DateTime Time)> pivots,
            double threshold,
            string levelType,
            string pairName,
            int maxClusters)
        {
            if (!pivots.Any()) return new List<SupportResistanceLevel>();

            // Sort by price
            var sorted = pivots.OrderBy(p => p.Price).ToList();
            var clusters = new List<List<(int Index, double Price, DateTime Time)>>();

            foreach (var pivot in sorted)
            {
                var matchingCluster = clusters.FirstOrDefault(c =>
                    Math.Abs(c.Average(p => p.Price) - pivot.Price) <= threshold);

                if (matchingCluster != null)
                    matchingCluster.Add(pivot);
                else
                    clusters.Add(new List<(int, double, DateTime)> { pivot });
            }

            // Convert clusters to levels, sorted by strength descending, take top maxClusters
            return clusters
                .Select(cluster =>
                {
                    // Recency-weighted average: recent pivots (top half by index) weight 2x
                    int medianIdx = cluster.Count / 2;
                    var sortedByIdx = cluster.OrderBy(p => p.Index).ToList();
                    double weightedSum = 0;
                    double totalWeight = 0;
                    for (int i = 0; i < sortedByIdx.Count; i++)
                    {
                        double weight = i >= medianIdx ? 2.0 : 1.0;
                        weightedSum += sortedByIdx[i].Price * weight;
                        totalWeight += weight;
                    }

                    return new SupportResistanceLevel
                    {
                        PairName        = pairName,
                        PriceLevel      = weightedSum / totalWeight,
                        LevelType       = levelType,
                        Strength        = cluster.Count,
                        FirstDetectedAt = cluster.Min(p => p.Time),
                        LastTestedAt    = cluster.Max(p => p.Time),
                        IsActive        = true
                    };
                })
                .OrderByDescending(l => l.Strength)
                .Take(maxClusters)
                .ToList();
        }
    }
}

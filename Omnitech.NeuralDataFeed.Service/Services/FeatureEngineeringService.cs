using Microsoft.Extensions.Logging;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using Skender.Stock.Indicators;

namespace Omnitech.NeuralDataFeed.Service.Services
{
    public class FeatureEngineeringService : IFeatureEngineeringService
    {
        private const int BatchSize = 50_000;
        private const int WarmupLookback = 200;

        private readonly ILogger<FeatureEngineeringService> _logger;
        private readonly IMarketDataTfRepository _marketDataTfRepository;
        private readonly IMarketDataFeaturesRepository _featuresRepository;
        private readonly ISupportResistanceLevelRepository _srRepository;

        public FeatureEngineeringService(
            ILogger<FeatureEngineeringService> logger,
            IMarketDataTfRepository marketDataTfRepository,
            IMarketDataFeaturesRepository featuresRepository,
            ISupportResistanceLevelRepository srRepository)
        {
            _logger = logger;
            _marketDataTfRepository = marketDataTfRepository;
            _featuresRepository = featuresRepository;
            _srRepository = srRepository;
        }

        public async Task CalculateFeaturesAsync(string pairName, string timeframe)
        {
            try
            {
                _logger.LogInformation("{Pair}/{Tf}: starting feature engineering", pairName, timeframe);

                var srLevels = await _srRepository.GetActiveLevelsAsync(pairName);
                var supports = srLevels.Where(l => l.LevelType == "support").ToList();
                var resistances = srLevels.Where(l => l.LevelType == "resistance").ToList();

                // Process in time-ordered batches
                DateTime? batchStart = null;
                int totalProcessed = 0;

                while (true)
                {
                    // Fetch candles: warmup + batch
                    List<MarketDataTf> candles;
                    if (batchStart == null)
                    {
                        candles = await _marketDataTfRepository.GetLastNCandlesAsync(pairName, timeframe, WarmupLookback + BatchSize);
                        if (candles.Count <= WarmupLookback) break;
                    }
                    else
                    {
                        // Get warmup candles before batchStart
                        var warmup = await _marketDataTfRepository.GetCandlesAsync(
                            pairName, timeframe,
                            DateTime.MinValue, batchStart.Value.AddMilliseconds(-1));
                        var lastWarmup = warmup.TakeLast(WarmupLookback).ToList();

                        var batchEnd = batchStart.Value.AddDays(365);
                        var batch = await _marketDataTfRepository.GetCandlesAsync(
                            pairName, timeframe, batchStart.Value, batchEnd);

                        if (!batch.Any()) break;
                        candles = lastWarmup.Concat(batch.Take(BatchSize)).ToList();
                    }

                    var quotes = candles.Select(c => (IQuote)new Quote
                    {
                        Date   = c.CandleOpenTime,
                        Open   = (decimal)c.OpenPrice,
                        High   = (decimal)c.HighPrice,
                        Low    = (decimal)c.LowPrice,
                        Close  = (decimal)c.ClosePrice,
                        Volume = (decimal)c.Volume
                    }).ToList();

                    // Compute indicators
                    var rsi14       = quotes.GetRsi(14).ToList();
                    var rsi7        = quotes.GetRsi(7).ToList();
                    var stochRsi    = quotes.GetStochRsi(14, 14, 3, 3).ToList();
                    var roc         = quotes.GetRoc(14).ToList();
                    var ema9        = quotes.GetEma(9).ToList();
                    var ema21       = quotes.GetEma(21).ToList();
                    var ema50       = quotes.GetEma(50).ToList();
                    var ema200      = quotes.GetEma(200).ToList();
                    var macd        = quotes.GetMacd(12, 26, 9).ToList();
                    var adx         = quotes.GetAdx(14).ToList();
                    var bb          = quotes.GetBollingerBands(20, 2).ToList();
                    var atr         = quotes.GetAtr(14).ToList();
                    var obv         = quotes.GetObv().ToList();
                    var vwap        = quotes.GetVwap().ToList();
                    var volSma      = quotes.GetSma(20).ToList();

                    // CMF (20)
                    var cmf         = quotes.GetCmf(20).ToList();

                    // Determine which candles belong to the non-warmup batch
                    int startIdx = batchStart == null ? WarmupLookback : WarmupLookback;
                    var targetCandles = candles.Skip(startIdx).ToList();

                    var features = new List<MarketDataFeature>(targetCandles.Count);

                    for (int i = 0; i < targetCandles.Count; i++)
                    {
                        int qi = i + startIdx; // index in full quotes list
                        var candle = targetCandles[i];

                        double? prevMacdHist = qi > 0 ? (double?)macd[qi - 1].Histogram : null;
                        double? curMacdHist  = (double?)macd[qi].Histogram;

                        var feature = new MarketDataFeature
                        {
                            PairName      = candle.PairName,
                            CandleOpenTime = candle.CandleOpenTime,
                            Timeframe     = candle.Timeframe,
                            OpenPrice     = candle.OpenPrice,
                            HighPrice     = candle.HighPrice,
                            LowPrice      = candle.LowPrice,
                            ClosePrice    = candle.ClosePrice,
                            Volume        = candle.Volume,
                            Rsi14         = (double?)rsi14[qi].Rsi,
                            Rsi7          = (double?)rsi7[qi].Rsi,
                            StochRsiK     = (double?)stochRsi[qi].StochRsi,
                            StochRsiD     = (double?)stochRsi[qi].Signal,
                            Roc14         = (double?)roc[qi].Roc,
                            Ema9          = (double?)ema9[qi].Ema,
                            Ema21         = (double?)ema21[qi].Ema,
                            Ema50         = (double?)ema50[qi].Ema,
                            Ema200        = (double?)ema200[qi].Ema,
                            MacdLine      = (double?)macd[qi].Macd,
                            MacdSignal    = (double?)macd[qi].Signal,
                            MacdHistogram = curMacdHist,
                            Adx14         = (double?)adx[qi].Adx,
                            BbUpper       = (double?)bb[qi].UpperBand,
                            BbMiddle      = (double?)bb[qi].Sma,
                            BbLower       = (double?)bb[qi].LowerBand,
                            BbPctb        = (double?)bb[qi].PercentB,
                            Atr14         = (double?)atr[qi].Atr,
                            Obv           = (double?)obv[qi].Obv,
                            Vwap          = (double?)vwap[qi].Vwap,
                            VolumeSma20   = (double?)volSma[qi].Sma,
                            Cmf20         = (double?)cmf[qi].Cmf,
                            PriceEma9Ratio  = ema9[qi].Ema.HasValue  ? candle.ClosePrice / (double)ema9[qi].Ema!.Value : null,
                            PriceEma21Ratio = ema21[qi].Ema.HasValue ? candle.ClosePrice / (double)ema21[qi].Ema!.Value : null,
                            MacdHistSlope   = (curMacdHist.HasValue && prevMacdHist.HasValue) ? curMacdHist - prevMacdHist : null
                        };

                        ApplySupportResistanceFeatures(feature, supports, resistances);
                        features.Add(feature);
                    }

                    await _featuresRepository.BulkUpsertAsync(features);
                    totalProcessed += features.Count;
                    _logger.LogInformation("{Pair}/{Tf}: upserted {Count} feature rows (total {Total})",
                        pairName, timeframe, features.Count, totalProcessed);

                    // If we got fewer than BatchSize new candles, we're done
                    if (targetCandles.Count < BatchSize) break;

                    batchStart = targetCandles.Last().CandleOpenTime.AddMilliseconds(1);
                }

                _logger.LogInformation("{Pair}/{Tf}: feature engineering complete, {Total} rows", pairName, timeframe, totalProcessed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Pair}/{Tf}: feature engineering failed", pairName, timeframe);
                throw;
            }
        }

        private static void ApplySupportResistanceFeatures(
            MarketDataFeature feature,
            List<SupportResistanceLevel> supports,
            List<SupportResistanceLevel> resistances)
        {
            double close = feature.ClosePrice;

            var nearestSupport    = supports.Where(s => s.PriceLevel <= close).OrderByDescending(s => s.PriceLevel).FirstOrDefault();
            var nearestResistance = resistances.Where(r => r.PriceLevel >= close).OrderBy(r => r.PriceLevel).FirstOrDefault();

            feature.NearestSupport    = nearestSupport?.PriceLevel;
            feature.NearestResistance = nearestResistance?.PriceLevel;
            feature.SupportStrength   = nearestSupport?.Strength;
            feature.ResistanceStrength = nearestResistance?.Strength;

            if (nearestSupport != null && close > 0)
                feature.DistSupportPct = (close - nearestSupport.PriceLevel) / close * 100.0;

            if (nearestResistance != null && close > 0)
                feature.DistResistancePct = (nearestResistance.PriceLevel - close) / close * 100.0;

            if (feature.DistSupportPct.HasValue && feature.DistResistancePct.HasValue)
            {
                double denom = feature.DistSupportPct.Value + feature.DistResistancePct.Value;
                feature.SrZonePosition = denom > 0 ? feature.DistSupportPct.Value / denom : null;
            }

            var allLevels = supports.Concat(resistances);
            feature.NumSrWithin1Pct = allLevels.Count(l => Math.Abs(l.PriceLevel - close) / close * 100.0 <= 1.0);
        }
    }

    // Adapter to implement IQuote for Skender
    internal class Quote : IQuote
    {
        public DateTime Date   { get; set; }
        public decimal Open    { get; set; }
        public decimal High    { get; set; }
        public decimal Low     { get; set; }
        public decimal Close   { get; set; }
        public decimal Volume  { get; set; }
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Domain.Enumerators;
using Omnitech.NeuralDataFeed.Domain.ExternalApi.Binance.Payloads;
using Omnitech.NeuralDataFeed.Domain.Payloads;
using Omnitech.NeuralDataFeed.Provider.Configurations;
using Omnitech.NeuralDataFeed.Provider.Interfaces;
using Omnitech.NeuralDataFeed.Service.Interfaces;

namespace Omnitech.NeuralDataFeed.Service.Services
{
    public class MarketDataService : IMarketDataService
    {
        private const int MaxConcurrentPairs = 3;

        private readonly ILogger<MarketDataService> _logger;
        private readonly IMarketDataRepository _marketDataRepository;
        private readonly IMarketDataTfRepository _marketDataTfRepository;
        private readonly ITradingPairProvider _tradingPairProvider;
        private readonly IBinanceService _binanceService;
        private readonly IFeatureEngineeringService _featureEngineeringService;
        private readonly ISupportResistanceService _supportResistanceService;
        private readonly ISignalService _signalService;
        private readonly string _connectionString;

        public MarketDataService(
            ILogger<MarketDataService> logger,
            IMarketDataRepository marketDataRepository,
            IMarketDataTfRepository marketDataTfRepository,
            ITradingPairProvider tradingPairProvider,
            IBinanceService binanceService,
            IFeatureEngineeringService featureEngineeringService,
            ISupportResistanceService supportResistanceService,
            ISignalService signalService,
            IOptions<DatabaseSettings> databaseSettings)
        {
            _logger = logger;
            _marketDataRepository = marketDataRepository;
            _marketDataTfRepository = marketDataTfRepository;
            _tradingPairProvider = tradingPairProvider;
            _binanceService = binanceService;
            _featureEngineeringService = featureEngineeringService;
            _supportResistanceService = supportResistanceService;
            _signalService = signalService;
            _connectionString = databaseSettings.Value.MarketDataDatabase;
        }

        public async Task UpdateMarketDataAsync()
        {
            try
            {
                _logger.LogInformation("Starting market data update for all pairs");

                var tradingPairs = _tradingPairProvider.GetTradingPairs();
                await _binanceService.GetServerTime();

                using var semaphore = new SemaphoreSlim(MaxConcurrentPairs);
                var tasks = tradingPairs.Select(async pair =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        await ProcessPairAsync(pair);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);

                _logger.LogInformation("Market data update complete for all pairs");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Market data update failed");
            }
        }

        private async Task ProcessPairAsync(TradingPairSettings tradingPair)
        {
            try
            {
                _logger.LogInformation("{Pair}: starting ingestion for {Count} timeframes",
                    tradingPair.Name, tradingPair.Timeframes.Count);

                foreach (var timeframe in tradingPair.Timeframes)
                {
                    await IngestTimeframeAsync(tradingPair, timeframe);
                }

                // Update S/R from 1h candles first
                await _supportResistanceService.UpdateLevelsAsync(tradingPair.Name);

                // Compute features for all timeframes
                foreach (var timeframe in tradingPair.Timeframes)
                {
                    await _featureEngineeringService.CalculateFeaturesAsync(tradingPair.Name, timeframe);
                }

                // Label signals
                await _signalService.UpdateLabelsAsync(tradingPair.Name);

                _logger.LogInformation("{Pair}: pipeline complete", tradingPair.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Pair}: pair pipeline failed", tradingPair.Name);
            }
        }

        private async Task IngestTimeframeAsync(TradingPairSettings tradingPair, string timeframe)
        {
            var interval = CandleStickInterval.FromCode(timeframe);

            var startTime = DateTimeOffset.FromUnixTimeMilliseconds(tradingPair.FirstCandleUnixTimeMilliseconds).DateTime;
            var lastCandle = await _marketDataTfRepository.GetMostRecentCandleAsync(tradingPair.Name, timeframe);

            if (lastCandle != null)
                startTime = lastCandle.CandleOpenTime;

            var cutoff = DateTime.UtcNow.Subtract(GetIntervalDuration(timeframe));
            long totalFetched = 0;

            do
            {
                var nextStart = startTime.Add(GetIntervalDuration(timeframe));
                var startMs = (nextStart - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

                var candles = await _binanceService.GetCandlestickDataTf(new GetCandlestickDataPayload
                {
                    Symbol    = tradingPair.Name,
                    Interval  = interval,
                    StartTime = startMs
                }, timeframe);

                if (candles.Count == 0) break;

                await _marketDataTfRepository.BulkInsertAsync(candles);

                totalFetched += candles.Count;
                startTime = candles.Last().CandleOpenTime;

                _logger.LogInformation("{Pair}/{Tf}: fetched {Count} candles (total {Total}, up to {Time})",
                    tradingPair.Name, timeframe, candles.Count, totalFetched, startTime);

            } while (startTime < cutoff);
        }

        private static TimeSpan GetIntervalDuration(string timeframe) => timeframe switch
        {
            "1m"  => TimeSpan.FromMinutes(1),
            "5m"  => TimeSpan.FromMinutes(5),
            "15m" => TimeSpan.FromMinutes(15),
            "1h"  => TimeSpan.FromHours(1),
            _     => throw new ArgumentException($"Unknown timeframe: {timeframe}")
        };

        // Legacy method — operates on original market_data table for backwards compatibility
        public async Task UpdateTradingPairMarketData(TradingPairSettings tradingPair)
        {
            try
            {
                _logger.LogInformation("{Pair}: legacy 1m ingestion start", tradingPair.Name);

                var lastCandleTimeStamp = DateTimeOffset.FromUnixTimeMilliseconds(tradingPair.FirstCandleUnixTimeMilliseconds).DateTime;
                var lastCandle = await _marketDataRepository.GetMostRecentCandleFromPairAsync(tradingPair.Name);

                if (lastCandle != null)
                    lastCandleTimeStamp = lastCandle.CandleOpenTime;

                do
                {
                    var candlestickData = await _binanceService.GetCandlestickData(new GetCandlestickDataPayload
                    {
                        Symbol    = tradingPair.Name,
                        Interval  = CandleStickInterval.OneMinuteInterval,
                        StartTime = (lastCandleTimeStamp.AddMinutes(1) - new DateTime(1970, 1, 1)).TotalMilliseconds
                    });

                    if (candlestickData.Count == 0) break;

                    await _marketDataRepository.InsertMarketDataListAsync(candlestickData);
                    lastCandleTimeStamp = candlestickData.Last().CandleOpenTime;

                } while (lastCandleTimeStamp < DateTime.UtcNow.AddMinutes(-1));

                _logger.LogInformation("{Pair}: legacy 1m ingestion complete", tradingPair.Name);
            }
            catch
            {
                throw;
            }
        }

        public async Task<List<MarketData>> GetMarketDataAsync(NeuralDataFeedRequestPayload payload)
        {
            try
            {
                if (payload.StartDateTime == null && payload.EndDateTime == null)
                    throw new Exception("StartDateTime or EndDateTime is required");

                if (payload.StartDateTime == null)
                {
                    payload.EndDateTime = payload.EndDateTime!.Value.AddMinutes(1);
                    payload.StartDateTime = payload.EndDateTime!.Value.AddMinutes(-payload.NumberOfCandles);
                }

                if (payload.EndDateTime == null)
                    payload.EndDateTime = payload.StartDateTime!.Value.AddMinutes(payload.NumberOfCandles);

                return await _marketDataRepository.GetMarketDataAsync(
                    payload.PairName, payload.StartDateTime!.Value, payload.EndDateTime!.Value);
            }
            catch
            {
                throw;
            }
        }
    }
}

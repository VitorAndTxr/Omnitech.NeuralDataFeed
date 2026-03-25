using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Domain.ExternalApi.Binance.Payloads;
using Omnitech.NeuralDataFeed.Domain.ExternalApi.Binance.Responses;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using System.Collections.Concurrent;
using System.Globalization;

namespace Omnitech.NeuralDataFeed.Service.Services
{
    public class BinanceService : IBinanceService
    {
        private readonly ILogger<BinanceService> _logger;
        private readonly HttpClient _httpClient;

        public BinanceService(ILogger<BinanceService> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<DateTime?> GetServerTime()
        {
            try
            {
                var response = await _httpClient.GetAsync("time");

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    var serverTimeResponse = JsonConvert.DeserializeObject<ServerTimeResponse>(responseString);

                    if (serverTimeResponse == null)
                    {
                        _logger.LogError("BinanceService.GetServerTime - serverTimeResponse is null");
                        return null;
                    }

                    var serverTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                        .AddMilliseconds(serverTimeResponse.ServerTime);

                    _logger.LogInformation("BinanceService.GetServerTime - ServerTime: {Time}", serverTime.ToLocalTime());
                    return serverTime;
                }

                throw new Exception($"BinanceService.GetServerTime - Error: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BinanceService.GetServerTime");
                return null;
            }
        }

        public async Task<List<MarketData>> GetCandlestickData(GetCandlestickDataPayload payload)
        {
            try
            {
                string queryParams = $"?symbol={payload.Symbol}&interval={payload.Interval.Code}&limit={payload.Limit}&startTime={payload.StartTime}";

                var response = await _httpClient.GetAsync("uiKlines" + queryParams);

                if (response.IsSuccessStatusCode)
                {
                    var candlesticks = new ConcurrentBag<MarketData>();
                    var responseString = await response.Content.ReadAsStringAsync();
                    var unmapped = JsonConvert.DeserializeObject<List<object[]>>(responseString);

                    if (unmapped == null)
                        throw new Exception("unmapedCandlesticks is null");

                    Parallel.ForEach(unmapped, candle =>
                    {
                        candlesticks.Add(new MarketData
                        {
                            PairName       = payload.Symbol,
                            CandleOpenTime = DateTimeOffset.FromUnixTimeMilliseconds((long)candle[0]).DateTime,
                            OpenPrice      = float.Parse((string)candle[1], CultureInfo.InvariantCulture),
                            HighPrice      = float.Parse((string)candle[2], CultureInfo.InvariantCulture),
                            LowPrice       = float.Parse((string)candle[3], CultureInfo.InvariantCulture),
                            ClosePrice     = float.Parse((string)candle[4], CultureInfo.InvariantCulture),
                            Volume         = float.Parse((string)candle[5], CultureInfo.InvariantCulture),
                            CandleCloseTime = DateTimeOffset.FromUnixTimeMilliseconds((long)candle[6]).DateTime
                        });
                    });

                    return candlesticks.OrderBy(x => x.CandleOpenTime).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BinanceService.GetCandlestickData:");
            }

            return new List<MarketData>();
        }

        public async Task<List<MarketDataTf>> GetCandlestickDataTf(GetCandlestickDataPayload payload, string timeframe)
        {
            try
            {
                string queryParams = $"?symbol={payload.Symbol}&interval={payload.Interval.Code}&limit={payload.Limit}&startTime={payload.StartTime}";

                var response = await _httpClient.GetAsync("uiKlines" + queryParams);

                if (response.IsSuccessStatusCode)
                {
                    var candlesticks = new ConcurrentBag<MarketDataTf>();
                    var responseString = await response.Content.ReadAsStringAsync();
                    var unmapped = JsonConvert.DeserializeObject<List<object[]>>(responseString);

                    if (unmapped == null)
                        throw new Exception("unmapedCandlesticks is null");

                    Parallel.ForEach(unmapped, candle =>
                    {
                        candlesticks.Add(new MarketDataTf
                        {
                            PairName       = payload.Symbol,
                            Timeframe      = timeframe,
                            CandleOpenTime = DateTimeOffset.FromUnixTimeMilliseconds((long)candle[0]).DateTime,
                            OpenPrice      = float.Parse((string)candle[1], CultureInfo.InvariantCulture),
                            HighPrice      = float.Parse((string)candle[2], CultureInfo.InvariantCulture),
                            LowPrice       = float.Parse((string)candle[3], CultureInfo.InvariantCulture),
                            ClosePrice     = float.Parse((string)candle[4], CultureInfo.InvariantCulture),
                            Volume         = float.Parse((string)candle[5], CultureInfo.InvariantCulture),
                            CandleCloseTime = DateTimeOffset.FromUnixTimeMilliseconds((long)candle[6]).DateTime
                        });
                    });

                    return candlesticks.OrderBy(x => x.CandleOpenTime).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BinanceService.GetCandlestickDataTf:");
            }

            return new List<MarketDataTf>();
        }
    }
}

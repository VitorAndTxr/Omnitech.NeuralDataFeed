using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Domain.ExternalApi.Binance.Payloads;
using Omnitech.NeuralDataFeed.Domain.ExternalApi.Binance.Responses;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection.Metadata;

namespace Omnitech.NeuralDataFeed.Service.Services
{
    public class BinanceService : IBinanceService
    {
        private readonly ILogger<BinanceService> _logger;
        private readonly string _apiBaseUrl = "https://api.binance.com/api/v3";

        public BinanceService(ILogger<BinanceService> logger)
        {
            _logger = logger;
        }

        public async Task<DateTime?> GetServerTime()
        {
            try
            {
                using HttpClient client = new HttpClient();

                HttpResponseMessage response = await client.GetAsync(_apiBaseUrl + "/time");

                if (response.IsSuccessStatusCode)
                {
                    DateTime serverTime;

                    var responseString = await response.Content.ReadAsStringAsync();

                    var serverTimeResponse = JsonConvert.DeserializeObject<ServerTimeResponse>(responseString);

                    if (serverTimeResponse == null)
                    {
                        _logger.LogError("BinanceService.GetServerTime - serverTimeResponse is null");
                        return null;
                    }

                    serverTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(serverTimeResponse.ServerTime);

                    _logger.LogInformation($"BinanceService.GetServerTime - ServerTime: {serverTime.ToLocalTime().ToString()}");

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
                using HttpClient client = new HttpClient();

                string queryParams = "?symbol=" + payload.Symbol + "&interval=" + payload.Interval.Code + "&limit=" + payload.Limit + "&startTime="+payload.StartTime;

                
                HttpResponseMessage response = await client.GetAsync(_apiBaseUrl + "/uiKlines" + queryParams);

                if (response.IsSuccessStatusCode)
                {
                    ConcurrentBag<MarketData> candlesticks = new ConcurrentBag<MarketData>();
                    var responseString = await response.Content.ReadAsStringAsync();

                    var unmapedCandlesticks = JsonConvert.DeserializeObject<List<object[]>>(responseString);

                    if (unmapedCandlesticks == null)
                    {
                        throw new Exception("unmapedCandlesticks is null");
                    }

                    Parallel.ForEach(unmapedCandlesticks, candle => {
                        var toAdd = new MarketData(); 

                        toAdd.PairName = payload.Symbol;
                        toAdd.CandleOpenTime = DateTimeOffset.FromUnixTimeMilliseconds((long)candle[0]).DateTime;
                        
                        toAdd.OpenPrice = float.Parse((string)candle[1], CultureInfo.InvariantCulture.NumberFormat);
                        toAdd.HighPrice = float.Parse((string)candle[2], CultureInfo.InvariantCulture.NumberFormat);
                        toAdd.LowPrice = float.Parse((string)candle[3], CultureInfo.InvariantCulture.NumberFormat);
                        toAdd.ClosePrice = float.Parse((string)candle[4], CultureInfo.InvariantCulture.NumberFormat);
                        toAdd.Volume = float.Parse((string)candle[5], CultureInfo.InvariantCulture.NumberFormat);

                        toAdd.CandleCloseTime = DateTimeOffset.FromUnixTimeMilliseconds((long)candle[6]).DateTime;

                        candlesticks.Add(toAdd);
                    });

                    return candlesticks.OrderBy(x=> x.CandleOpenTime).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BinanceService.GetCandlestickData:");
            }
            return new List<MarketData>();
        }
    }
}

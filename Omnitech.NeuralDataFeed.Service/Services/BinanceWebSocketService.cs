using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Provider.Interfaces;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Omnitech.NeuralDataFeed.Service.Services
{
    public class BinanceWebSocketService : IBinanceWebSocketService
    {
        private const string BaseUrl = "wss://stream.binance.com:9443/stream?streams=";
        private const int PingIntervalSeconds = 30;
        private const int MaxBackoffSeconds = 60;

        private readonly ILogger<BinanceWebSocketService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        // ITradingPairProvider is used only for building the stream URL; resolve once per connection attempt via a transient scope
        private readonly ITradingPairProvider _tradingPairProvider;

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;

        public BinanceWebSocketService(
            ILogger<BinanceWebSocketService> logger,
            IServiceScopeFactory scopeFactory,
            ITradingPairProvider tradingPairProvider)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _tradingPairProvider = tradingPairProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await ConnectWithRetryAsync(_cts.Token);
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            if (_webSocket?.State == WebSocketState.Open)
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopping", CancellationToken.None);
        }

        private string BuildStreamUrl()
        {
            var pairs = _tradingPairProvider.GetTradingPairs();
            var streams = pairs
                .SelectMany(p => p.Timeframes.Select(tf => $"{p.Name.ToLower()}@kline_{tf}"))
                .ToList();
            return BaseUrl + string.Join("/", streams);
        }

        private async Task ConnectWithRetryAsync(CancellationToken token)
        {
            int backoff = 1;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    _webSocket = new ClientWebSocket();
                    var uri = new Uri(BuildStreamUrl());
                    await _webSocket.ConnectAsync(uri, token);
                    _logger.LogInformation("Binance WebSocket connected");
                    backoff = 1;

                    await Task.WhenAll(
                        ReceiveLoopAsync(token),
                        PingLoopAsync(token));
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WebSocket error, reconnecting in {Backoff}s", backoff);
                    await Task.Delay(TimeSpan.FromSeconds(backoff), token);
                    backoff = Math.Min(backoff * 2, MaxBackoffSeconds);
                }
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[16 * 1024];
            var messageBuilder = new StringBuilder();

            while (_webSocket!.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("WebSocket closed by server");
                    break;
                }

                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    var message = messageBuilder.ToString();
                    messageBuilder.Clear();
                    await ProcessMessageAsync(message);
                }
            }
        }

        private async Task PingLoopAsync(CancellationToken token)
        {
            while (_webSocket!.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(PingIntervalSeconds), token);
                if (_webSocket.State == WebSocketState.Open)
                {
                    var ping = Encoding.UTF8.GetBytes("{\"method\":\"ping\"}");
                    await _webSocket.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, token);
                }
            }
        }

        private async Task ProcessMessageAsync(string message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;

                if (!root.TryGetProperty("data", out var data)) return;
                if (!data.TryGetProperty("k", out var k)) return;

                // Only process closed candles
                if (!k.GetProperty("x").GetBoolean()) return;

                var symbol    = data.GetProperty("s").GetString()!;
                var interval  = k.GetProperty("i").GetString()!;
                var openTime  = DateTimeOffset.FromUnixTimeMilliseconds(k.GetProperty("t").GetInt64()).DateTime;
                var closeTime = DateTimeOffset.FromUnixTimeMilliseconds(k.GetProperty("T").GetInt64()).DateTime;

                var candle = new MarketDataTf
                {
                    PairName        = symbol,
                    Timeframe       = interval,
                    CandleOpenTime  = openTime,
                    CandleCloseTime = closeTime,
                    OpenPrice       = double.Parse(k.GetProperty("o").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    HighPrice       = double.Parse(k.GetProperty("h").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    LowPrice        = double.Parse(k.GetProperty("l").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    ClosePrice      = double.Parse(k.GetProperty("c").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                    Volume          = double.Parse(k.GetProperty("v").GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                };

                // Create a scope per closed candle to safely resolve Scoped services from this Singleton
                using var scope = _scopeFactory.CreateScope();
                var repo    = scope.ServiceProvider.GetRequiredService<IMarketDataTfRepository>();
                var featSvc = scope.ServiceProvider.GetRequiredService<IFeatureEngineeringService>();

                await repo.BulkInsertAsync(new[] { candle });
                await featSvc.CalculateFeaturesAsync(symbol, interval);

                _logger.LogDebug("WebSocket: processed closed candle {Symbol}/{Interval} at {Time}", symbol, interval, openTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket: failed to process message");
            }
        }
    }
}

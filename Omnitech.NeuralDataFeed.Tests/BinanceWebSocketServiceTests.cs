using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Provider.Interfaces;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using Omnitech.NeuralDataFeed.Service.Services;

namespace Omnitech.NeuralDataFeed.Tests;

public class BinanceWebSocketServiceTests
{
    // We test the internal ProcessMessageAsync logic by invoking it via reflection,
    // since the public API requires a real WebSocket connection.
    private static BinanceWebSocketService BuildService(
        Mock<IMarketDataTfRepository> repoMock,
        Mock<IFeatureEngineeringService> featSvcMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repoMock.Object);
        services.AddSingleton(featSvcMock.Object);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var pairProvider = new Mock<ITradingPairProvider>();
        pairProvider.Setup(p => p.GetTradingPairs()).Returns(new List<TradingPairSettings>
        {
            new() { Name = "BTCUSDT", Timeframes = new List<string> { "5m" } }
        });

        return new BinanceWebSocketService(
            NullLogger<BinanceWebSocketService>.Instance,
            scopeFactory,
            pairProvider.Object);
    }

    private static Task InvokeProcessMessage(BinanceWebSocketService service, string message)
    {
        var method = typeof(BinanceWebSocketService)
            .GetMethod("ProcessMessageAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(service, new object[] { message })!;
    }

    private static string ClosedKlineJson(bool isClosed = true) =>
        $$"""
        {
          "stream": "btcusdt@kline_5m",
          "data": {
            "e": "kline",
            "E": 1700000000000,
            "s": "BTCUSDT",
            "k": {
              "t": 1699999800000,
              "T": 1699999999999,
              "s": "BTCUSDT",
              "i": "5m",
              "f": 100,
              "L": 200,
              "o": "50000.00",
              "c": "50100.00",
              "h": "50200.00",
              "l": "49900.00",
              "v": "10.5",
              "n": 100,
              "x": {{(isClosed ? "true" : "false")}},
              "q": "525000.00",
              "V": "5.0",
              "Q": "250000.00",
              "B": "0"
            }
          }
        }
        """;

    [Fact]
    public async Task ProcessMessageAsync_ClosedCandle_InsertsAndTriggersFeatureCalc()
    {
        var repoMock = new Mock<IMarketDataTfRepository>();
        repoMock.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MarketDataTf>>()))
            .Returns(Task.CompletedTask);

        var featMock = new Mock<IFeatureEngineeringService>();
        featMock.Setup(f => f.CalculateFeaturesAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(repoMock, featMock);
        await InvokeProcessMessage(service, ClosedKlineJson(isClosed: true));

        repoMock.Verify(r => r.BulkInsertAsync(
            It.Is<IEnumerable<MarketDataTf>>(c => c.Any())), Times.Once);
        featMock.Verify(f => f.CalculateFeaturesAsync("BTCUSDT", "5m"), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_OpenCandle_IgnoresMessage()
    {
        var repoMock = new Mock<IMarketDataTfRepository>();
        var featMock = new Mock<IFeatureEngineeringService>();
        var service = BuildService(repoMock, featMock);

        await InvokeProcessMessage(service, ClosedKlineJson(isClosed: false));

        repoMock.Verify(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MarketDataTf>>()), Times.Never);
        featMock.Verify(f => f.CalculateFeaturesAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_MessageWithoutDataProperty_IgnoresMessage()
    {
        var repoMock = new Mock<IMarketDataTfRepository>();
        var featMock = new Mock<IFeatureEngineeringService>();
        var service = BuildService(repoMock, featMock);

        await InvokeProcessMessage(service, """{"other":"value"}""");

        repoMock.Verify(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MarketDataTf>>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_ClosedCandle_ParsedCorrectly()
    {
        MarketDataTf? inserted = null;
        var repoMock = new Mock<IMarketDataTfRepository>();
        repoMock.Setup(r => r.BulkInsertAsync(It.IsAny<IEnumerable<MarketDataTf>>()))
            .Callback<IEnumerable<MarketDataTf>>(candles => inserted = candles.FirstOrDefault())
            .Returns(Task.CompletedTask);

        var featMock = new Mock<IFeatureEngineeringService>();
        featMock.Setup(f => f.CalculateFeaturesAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(repoMock, featMock);
        await InvokeProcessMessage(service, ClosedKlineJson(isClosed: true));

        Assert.NotNull(inserted);
        Assert.Equal("BTCUSDT", inserted!.PairName);
        Assert.Equal("5m", inserted.Timeframe);
        Assert.Equal(50000.0, inserted.OpenPrice);
        Assert.Equal(50100.0, inserted.ClosePrice);
        Assert.Equal(50200.0, inserted.HighPrice);
        Assert.Equal(49900.0, inserted.LowPrice);
        Assert.Equal(10.5, inserted.Volume);
    }

    [Fact]
    public void BuildStreamUrl_IncludesAllPairTimeframeCombinations()
    {
        var pairProvider = new Mock<ITradingPairProvider>();
        pairProvider.Setup(p => p.GetTradingPairs()).Returns(new List<TradingPairSettings>
        {
            new() { Name = "BTCUSDT", Timeframes = new List<string> { "5m", "15m", "1h" } },
            new() { Name = "ETHUSDT", Timeframes = new List<string> { "5m" } }
        });

        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

        var svc = new BinanceWebSocketService(
            NullLogger<BinanceWebSocketService>.Instance,
            scopeFactory,
            pairProvider.Object);

        var method = typeof(BinanceWebSocketService)
            .GetMethod("BuildStreamUrl", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var url = (string)method.Invoke(svc, null)!;

        Assert.Contains("btcusdt@kline_5m", url);
        Assert.Contains("btcusdt@kline_15m", url);
        Assert.Contains("btcusdt@kline_1h", url);
        Assert.Contains("ethusdt@kline_5m", url);
    }
}

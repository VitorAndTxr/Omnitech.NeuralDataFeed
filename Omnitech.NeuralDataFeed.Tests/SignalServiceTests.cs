using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Provider.Interfaces;
using Omnitech.NeuralDataFeed.Service.Services;

namespace Omnitech.NeuralDataFeed.Tests;

public class SignalServiceTests
{
    private static MarketDataFeature MakeFeature(double close, double high, double low) => new()
    {
        PairName = "BTCUSDT",
        Timeframe = "5m",
        CandleOpenTime = DateTime.UtcNow,
        ClosePrice = close,
        HighPrice = high,
        LowPrice = low,
        OpenPrice = close,
        Volume = 1
    };

    private SignalService BuildService(
        List<MarketDataFeature> unlabeled,
        string pairName = "BTCUSDT",
        string timeframe = "5m",
        double targetPct = 1.6,
        double stopPct = 0.4)
    {
        var featRepo = new Mock<IMarketDataFeaturesRepository>();
        // Return unlabeled on first call, empty on second to stop the loop
        featRepo.SetupSequence(r => r.GetUnlabeledAsync(pairName, timeframe, It.IsAny<int>()))
            .ReturnsAsync(unlabeled)
            .ReturnsAsync(new List<MarketDataFeature>());
        featRepo.Setup(r => r.UpdateLabelsAsync(It.IsAny<IEnumerable<MarketDataFeature>>()))
            .Returns(Task.CompletedTask);

        var pairProvider = new Mock<ITradingPairProvider>();
        pairProvider.Setup(p => p.GetTradingPairs()).Returns(new List<TradingPairSettings>
        {
            new() { Name = pairName, Timeframes = new List<string> { timeframe } }
        });

        var labelingProvider = new Mock<ILabelingProvider>();
        labelingProvider.Setup(l => l.GetThreshold(pairName, timeframe))
            .Returns(new LabelingThreshold { TargetPercent = targetPct, StopPercent = stopPct });

        return new SignalService(
            NullLogger<SignalService>.Instance,
            featRepo.Object,
            pairProvider.Object,
            labelingProvider.Object);
    }

    [Fact]
    public async Task UpdateLabelsAsync_TargetHitFirst_SetsBuySignalTrue()
    {
        // Arrange: ref close = 100, target = 1.6% => 101.6, future candle high = 102
        var candles = new List<MarketDataFeature>
        {
            MakeFeature(close: 100, high: 100.5, low: 99.8),
            MakeFeature(close: 101, high: 102.0, low: 100.5),  // target hit (high > 101.6)
            MakeFeature(close: 101, high: 102.0, low: 100.5),
        };

        var service = BuildService(candles);
        await service.UpdateLabelsAsync("BTCUSDT");

        Assert.True(candles[0].BuySignal);
        Assert.False(candles[0].SellSignal);
        Assert.NotNull(candles[0].TargetPct);
        Assert.NotNull(candles[0].DrawdownPct);
    }

    [Fact]
    public async Task UpdateLabelsAsync_StopHitFirst_SetsSellSignalTrue()
    {
        // ref close = 100, stop = 0.4% => 99.6, future candle low = 99.0
        var candles = new List<MarketDataFeature>
        {
            MakeFeature(close: 100, high: 100.2, low: 99.9),
            MakeFeature(close: 99.5, high: 99.8, low: 99.0), // stop hit (low < 99.6)
            MakeFeature(close: 99.5, high: 99.8, low: 99.0),
        };

        var service = BuildService(candles);
        await service.UpdateLabelsAsync("BTCUSDT");

        Assert.False(candles[0].BuySignal);
        Assert.True(candles[0].SellSignal);
    }

    [Fact]
    public async Task UpdateLabelsAsync_NoTargetOrStopHit_LeavesLabelNull()
    {
        // All candles stay within ±0.1% of reference
        var candles = new List<MarketDataFeature>
        {
            MakeFeature(close: 100, high: 100.05, low: 99.95),
            MakeFeature(close: 100, high: 100.08, low: 99.92),
        };

        var service = BuildService(candles);
        await service.UpdateLabelsAsync("BTCUSDT");

        // Last candle has no future candles to scan against, so both remain unlabeled
        Assert.Null(candles[1].BuySignal);
        Assert.Null(candles[1].SellSignal);
    }

    [Fact]
    public async Task UpdateLabelsAsync_BothHitSameCandle_UsesOpenProximityToDetermineDirection()
    {
        // Open >= ref => buySignal = true
        var candles = new List<MarketDataFeature>
        {
            MakeFeature(close: 100, high: 100.2, low: 99.9),
            // both stop and target in same candle; open = 100 >= refPrice => buy
            new MarketDataFeature
            {
                PairName = "BTCUSDT", Timeframe = "5m",
                CandleOpenTime = DateTime.UtcNow,
                ClosePrice = 100, HighPrice = 102.0, LowPrice = 99.0,
                OpenPrice = 100.0, Volume = 1
            }
        };

        var service = BuildService(candles);
        await service.UpdateLabelsAsync("BTCUSDT");

        Assert.True(candles[0].BuySignal);
    }

    [Fact]
    public async Task UpdateLabelsAsync_UnknownPair_ReturnsWithoutProcessing()
    {
        var featRepo = new Mock<IMarketDataFeaturesRepository>();
        var pairProvider = new Mock<ITradingPairProvider>();
        pairProvider.Setup(p => p.GetTradingPairs()).Returns(new List<TradingPairSettings>());
        var labelingProvider = new Mock<ILabelingProvider>();

        var service = new SignalService(
            NullLogger<SignalService>.Instance,
            featRepo.Object, pairProvider.Object, labelingProvider.Object);

        // Should not throw, and should not call repository
        await service.UpdateLabelsAsync("UNKNOWN");

        featRepo.Verify(r => r.GetUnlabeledAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
}

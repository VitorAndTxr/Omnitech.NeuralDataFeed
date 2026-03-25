using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Service.Services;

namespace Omnitech.NeuralDataFeed.Tests;

public class SupportResistanceServiceTests
{
    private static MarketDataTf MakeCandle(DateTime time, double open, double high, double low, double close, double volume = 1000)
        => new()
        {
            PairName = "BTCUSDT",
            Timeframe = "1h",
            CandleOpenTime = time,
            CandleCloseTime = time.AddHours(1),
            OpenPrice = open,
            HighPrice = high,
            LowPrice = low,
            ClosePrice = close,
            Volume = volume
        };

    /// <summary>
    /// Generates a smooth price series of 100 candles with one clear pivot high and one clear pivot low
    /// embedded so fractal detection can fire.
    /// </summary>
    private static List<MarketDataTf> MakeCandlesWithClearPivots()
    {
        var candles = new List<MarketDataTf>();
        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 100; i++)
        {
            double price = 50000 + Math.Sin(i * 0.3) * 200; // smooth oscillation
            candles.Add(MakeCandle(
                baseTime.AddHours(i),
                open: price,
                high: price + 50,
                low: price - 50,
                close: price));
        }

        // Inject a spike at index 50 so it becomes a clear pivot high with N=5 neighbors
        candles[50] = MakeCandle(baseTime.AddHours(50),
            open: 52000, high: 53000, low: 51500, close: 52000);

        // Inject a dip at index 30 so it becomes a clear pivot low
        candles[30] = MakeCandle(baseTime.AddHours(30),
            open: 47500, high: 48000, low: 46500, close: 47500);

        return candles;
    }

    private SupportResistanceService BuildService(
        List<MarketDataTf> candles,
        Mock<ISupportResistanceLevelRepository>? srRepoMock = null)
    {
        var mdRepo = new Mock<IMarketDataTfRepository>();
        mdRepo.Setup(r => r.GetLastNCandlesAsync("BTCUSDT", "1h", It.IsAny<int>()))
            .ReturnsAsync(candles);

        srRepoMock ??= new Mock<ISupportResistanceLevelRepository>();
        srRepoMock.Setup(r => r.DeactivateStaleAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        srRepoMock.Setup(r => r.UpsertLevelsAsync(It.IsAny<IEnumerable<SupportResistanceLevel>>()))
            .Returns(Task.CompletedTask);

        return new SupportResistanceService(
            NullLogger<SupportResistanceService>.Instance,
            mdRepo.Object,
            srRepoMock.Object);
    }

    [Fact]
    public async Task UpdateLevelsAsync_InsufficientCandles_SkipsProcessing()
    {
        var candles = Enumerable.Range(0, 30)
            .Select(i => MakeCandle(DateTime.UtcNow.AddHours(i), 50000, 50100, 49900, 50000))
            .ToList();

        var srRepo = new Mock<ISupportResistanceLevelRepository>();
        srRepo.Setup(r => r.DeactivateStaleAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(candles, srRepo);

        await service.UpdateLevelsAsync("BTCUSDT");

        srRepo.Verify(r => r.UpsertLevelsAsync(It.IsAny<IEnumerable<SupportResistanceLevel>>()), Times.Never);
    }

    [Fact]
    public async Task UpdateLevelsAsync_WithClearPivots_CallsUpsertWithLevels()
    {
        var candles = MakeCandlesWithClearPivots();
        var srRepo = new Mock<ISupportResistanceLevelRepository>();
        srRepo.Setup(r => r.DeactivateStaleAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        srRepo.Setup(r => r.UpsertLevelsAsync(It.IsAny<IEnumerable<SupportResistanceLevel>>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(candles, srRepo);
        await service.UpdateLevelsAsync("BTCUSDT");

        // With clear pivots, upsert should be called with at least one level
        srRepo.Verify(r => r.UpsertLevelsAsync(
            It.Is<IEnumerable<SupportResistanceLevel>>(levels => levels.Any())), Times.Once);
    }

    [Fact]
    public async Task UpdateLevelsAsync_MaxActiveLevelsRespected()
    {
        var candles = MakeCandlesWithClearPivots();

        IEnumerable<SupportResistanceLevel>? capturedLevels = null;
        var srRepo = new Mock<ISupportResistanceLevelRepository>();
        srRepo.Setup(r => r.DeactivateStaleAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        srRepo.Setup(r => r.UpsertLevelsAsync(It.IsAny<IEnumerable<SupportResistanceLevel>>()))
            .Callback<IEnumerable<SupportResistanceLevel>>(l => capturedLevels = l)
            .Returns(Task.CompletedTask);

        var service = BuildService(candles, srRepo);
        await service.UpdateLevelsAsync("BTCUSDT");

        if (capturedLevels != null)
        {
            var supports = capturedLevels.Where(l => l.LevelType == "support").ToList();
            var resistances = capturedLevels.Where(l => l.LevelType == "resistance").ToList();
            Assert.True(supports.Count <= 10, $"Too many support levels: {supports.Count}");
            Assert.True(resistances.Count <= 10, $"Too many resistance levels: {resistances.Count}");
        }
    }

    [Fact]
    public async Task UpdateLevelsAsync_LevelTypesAreCorrect()
    {
        var candles = MakeCandlesWithClearPivots();

        IEnumerable<SupportResistanceLevel>? capturedLevels = null;
        var srRepo = new Mock<ISupportResistanceLevelRepository>();
        srRepo.Setup(r => r.DeactivateStaleAsync(It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);
        srRepo.Setup(r => r.UpsertLevelsAsync(It.IsAny<IEnumerable<SupportResistanceLevel>>()))
            .Callback<IEnumerable<SupportResistanceLevel>>(l => capturedLevels = l)
            .Returns(Task.CompletedTask);

        var service = BuildService(candles, srRepo);
        await service.UpdateLevelsAsync("BTCUSDT");

        if (capturedLevels != null)
        {
            Assert.All(capturedLevels, l =>
                Assert.True(l.LevelType == "support" || l.LevelType == "resistance",
                    $"Unexpected level type: {l.LevelType}"));
        }
    }
}

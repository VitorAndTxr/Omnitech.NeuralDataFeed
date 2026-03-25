using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Service.Services;

namespace Omnitech.NeuralDataFeed.Tests;

public class FeatureEngineeringServiceTests
{
    private static MarketDataTf MakeCandle(int minuteOffset, double price = 50000)
    {
        var t = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(minuteOffset * 5);
        return new MarketDataTf
        {
            PairName = "BTCUSDT",
            Timeframe = "5m",
            CandleOpenTime = t,
            CandleCloseTime = t.AddMinutes(5),
            OpenPrice = price,
            HighPrice = price * 1.002,
            LowPrice = price * 0.998,
            ClosePrice = price,
            Volume = 100
        };
    }

    /// <summary>
    /// Generates 250 candles (enough warmup + one batch) with a gentle price trend.
    /// </summary>
    private static List<MarketDataTf> MakeSufficientCandles(int count = 250)
        => Enumerable.Range(0, count)
            .Select(i => MakeCandle(i, 50000 + i * 10))
            .ToList();

    private FeatureEngineeringService BuildService(
        List<MarketDataTf> candles,
        List<SupportResistanceLevel>? srLevels = null,
        Mock<IMarketDataFeaturesRepository>? featRepoMock = null)
    {
        srLevels ??= new List<SupportResistanceLevel>();

        var mdRepo = new Mock<IMarketDataTfRepository>();
        mdRepo.Setup(r => r.GetLastNCandlesAsync("BTCUSDT", "5m", It.IsAny<int>()))
            .ReturnsAsync(candles);
        // Subsequent batch calls (batchStart != null) return empty to stop the loop
        mdRepo.Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MarketDataTf>());

        var srRepo = new Mock<ISupportResistanceLevelRepository>();
        srRepo.Setup(r => r.GetActiveLevelsAsync("BTCUSDT"))
            .ReturnsAsync(srLevels);

        if (featRepoMock == null)
        {
            featRepoMock = new Mock<IMarketDataFeaturesRepository>();
            featRepoMock.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<MarketDataFeature>>()))
                .Returns(Task.CompletedTask);
        }

        return new FeatureEngineeringService(
            NullLogger<FeatureEngineeringService>.Instance,
            mdRepo.Object,
            featRepoMock.Object,
            srRepo.Object);
    }

    [Fact]
    public async Task CalculateFeaturesAsync_SufficientCandles_CallsBulkUpsert()
    {
        var candles = MakeSufficientCandles(250);
        var featRepo = new Mock<IMarketDataFeaturesRepository>();
        featRepo.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<MarketDataFeature>>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(candles, featRepoMock: featRepo);
        await service.CalculateFeaturesAsync("BTCUSDT", "5m");

        featRepo.Verify(r => r.BulkUpsertAsync(
            It.Is<IEnumerable<MarketDataFeature>>(f => f.Any())), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CalculateFeaturesAsync_InsufficientCandles_SkipsUpsert()
    {
        // Less than WarmupLookback (200)
        var candles = MakeSufficientCandles(150);
        var featRepo = new Mock<IMarketDataFeaturesRepository>();
        featRepo.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<MarketDataFeature>>()))
            .Returns(Task.CompletedTask);

        var service = BuildService(candles, featRepoMock: featRepo);
        await service.CalculateFeaturesAsync("BTCUSDT", "5m");

        featRepo.Verify(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<MarketDataFeature>>()), Times.Never);
    }

    [Fact]
    public async Task CalculateFeaturesAsync_ProducesFeatureWithCorrectPairAndTimeframe()
    {
        var candles = MakeSufficientCandles(250);
        var capturedFeatures = new List<MarketDataFeature>();

        var featRepo = new Mock<IMarketDataFeaturesRepository>();
        featRepo.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<MarketDataFeature>>()))
            .Returns<IEnumerable<MarketDataFeature>>(f => { capturedFeatures.AddRange(f); return Task.CompletedTask; });

        var service = BuildService(candles, featRepoMock: featRepo);
        await service.CalculateFeaturesAsync("BTCUSDT", "5m");

        Assert.NotEmpty(capturedFeatures);
        Assert.All(capturedFeatures, f =>
        {
            Assert.Equal("BTCUSDT", f.PairName);
            Assert.Equal("5m", f.Timeframe);
        });
    }

    [Fact]
    public async Task CalculateFeaturesAsync_WithSrLevels_PopulatesNearestSupportAndResistance()
    {
        var candles = MakeSufficientCandles(250);
        // close price is around 50000+, support at 49000, resistance at 55000+
        var srLevels = new List<SupportResistanceLevel>
        {
            new() { PairName = "BTCUSDT", PriceLevel = 49000, LevelType = "support", Strength = 3,
                    FirstDetectedAt = DateTime.UtcNow, LastTestedAt = DateTime.UtcNow, IsActive = true },
            new() { PairName = "BTCUSDT", PriceLevel = 55000, LevelType = "resistance", Strength = 2,
                    FirstDetectedAt = DateTime.UtcNow, LastTestedAt = DateTime.UtcNow, IsActive = true }
        };

        var capturedFeatures = new List<MarketDataFeature>();
        var featRepo = new Mock<IMarketDataFeaturesRepository>();
        featRepo.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<MarketDataFeature>>()))
            .Returns<IEnumerable<MarketDataFeature>>(f => { capturedFeatures.AddRange(f); return Task.CompletedTask; });

        var service = BuildService(candles, srLevels, featRepo);
        await service.CalculateFeaturesAsync("BTCUSDT", "5m");

        Assert.NotEmpty(capturedFeatures);
        var firstWithSr = capturedFeatures.FirstOrDefault(f => f.NearestSupport.HasValue);
        Assert.NotNull(firstWithSr);
        Assert.Equal(49000, firstWithSr!.NearestSupport!.Value, precision: 0);
        Assert.Equal(55000, firstWithSr.NearestResistance!.Value, precision: 0);
    }

    [Fact]
    public async Task CalculateFeaturesAsync_NoSrLevels_SrFeaturesAreNull()
    {
        var candles = MakeSufficientCandles(250);

        var capturedFeatures = new List<MarketDataFeature>();
        var featRepo = new Mock<IMarketDataFeaturesRepository>();
        featRepo.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<MarketDataFeature>>()))
            .Returns<IEnumerable<MarketDataFeature>>(f => { capturedFeatures.AddRange(f); return Task.CompletedTask; });

        var service = BuildService(candles, new List<SupportResistanceLevel>(), featRepo);
        await service.CalculateFeaturesAsync("BTCUSDT", "5m");

        Assert.NotEmpty(capturedFeatures);
        Assert.All(capturedFeatures, f =>
        {
            Assert.Null(f.NearestSupport);
            Assert.Null(f.NearestResistance);
        });
    }
}

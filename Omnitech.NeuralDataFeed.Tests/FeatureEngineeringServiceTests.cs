using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Service.Services;

namespace Omnitech.NeuralDataFeed.Tests;

public class FeatureEngineeringServiceTests
{
    private static MarketDataTf MakeCandle(int minuteOffset, double price = 50000, double volume = 100)
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
            Volume = volume
        };
    }

    /// <summary>
    /// Generates candles (400 warmup + some target) with a gentle price trend.
    /// Default 450 = 400 warmup + 50 target candles.
    /// </summary>
    private static List<MarketDataTf> MakeSufficientCandles(int count = 450)
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
        mdRepo.Setup(r => r.GetCandlesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(candles);

        var srRepo = new Mock<ISupportResistanceLevelRepository>();
        srRepo.Setup(r => r.GetAllLevelsAsync("BTCUSDT"))
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
        var candles = MakeSufficientCandles(450);
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
        // Less than WarmupLookback (400)
        var candles = MakeSufficientCandles(350);
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
        var candles = MakeSufficientCandles(450);
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
        var candles = MakeSufficientCandles(450);

        // S/R levels detected well before the first candle timestamp so they pass the temporal filter
        var epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var srLevels = new List<SupportResistanceLevel>
        {
            new() { PairName = "BTCUSDT", PriceLevel = 49000, LevelType = "support", Strength = 3,
                    FirstDetectedAt = epoch, LastTestedAt = epoch, IsActive = true },
            new() { PairName = "BTCUSDT", PriceLevel = 55000, LevelType = "resistance", Strength = 2,
                    FirstDetectedAt = epoch, LastTestedAt = epoch, IsActive = true }
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
        var candles = MakeSufficientCandles(450);

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

    // --- New tests for FIX-1, FIX-2, FIX-3, FIX-5 ---

    [Fact]
    public async Task CalculateFeaturesAsync_VolumeSma20_ReflectsVolumeNotClose()
    {
        // All candles have price=50000, volume=1000. If VolumeSma20 is correct, it should be ~1000, not ~50000.
        var candles = Enumerable.Range(0, 450)
            .Select(i => MakeCandle(i, price: 50000, volume: 1000))
            .ToList();

        var capturedFeatures = new List<MarketDataFeature>();
        var featRepo = new Mock<IMarketDataFeaturesRepository>();
        featRepo.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<MarketDataFeature>>()))
            .Returns<IEnumerable<MarketDataFeature>>(f => { capturedFeatures.AddRange(f); return Task.CompletedTask; });

        var service = BuildService(candles, featRepoMock: featRepo);
        await service.CalculateFeaturesAsync("BTCUSDT", "5m");

        Assert.NotEmpty(capturedFeatures);
        var withVolSma = capturedFeatures.Where(f => f.VolumeSma20.HasValue).ToList();
        Assert.NotEmpty(withVolSma);
        // Volume SMA should be ~1000 (volume scale), not ~50000 (price scale)
        Assert.All(withVolSma, f => Assert.InRange(f.VolumeSma20!.Value, 500, 2000));
    }

    [Fact]
    public async Task CalculateFeaturesAsync_SrLookAheadBias_FutureLevelsNotApplied()
    {
        var candles = MakeSufficientCandles(450);
        var firstCandleTime = candles[0].CandleOpenTime;

        // S/R level detected AFTER the dataset — should not be applied to any candle
        var futureLevel = new SupportResistanceLevel
        {
            PairName = "BTCUSDT", PriceLevel = 49500, LevelType = "support", Strength = 5,
            FirstDetectedAt = firstCandleTime.AddYears(1), LastTestedAt = firstCandleTime.AddYears(1), IsActive = true
        };

        var capturedFeatures = new List<MarketDataFeature>();
        var featRepo = new Mock<IMarketDataFeaturesRepository>();
        featRepo.Setup(r => r.BulkUpsertAsync(It.IsAny<IEnumerable<MarketDataFeature>>()))
            .Returns<IEnumerable<MarketDataFeature>>(f => { capturedFeatures.AddRange(f); return Task.CompletedTask; });

        var service = BuildService(candles, new List<SupportResistanceLevel> { futureLevel }, featRepo);
        await service.CalculateFeaturesAsync("BTCUSDT", "5m");

        Assert.NotEmpty(capturedFeatures);
        // Future S/R level should NOT be applied — NearestSupport must be null for all candles
        Assert.All(capturedFeatures, f => Assert.Null(f.NearestSupport));
    }
}
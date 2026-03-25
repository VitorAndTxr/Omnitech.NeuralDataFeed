using Microsoft.Extensions.Options;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Provider.Providers;

namespace Omnitech.NeuralDataFeed.Tests;

public class LabelingProviderTests
{
    private static LabelingProvider BuildProvider(LabelingSettings settings)
    {
        var options = Options.Create(settings);
        return new LabelingProvider(options);
    }

    private static LabelingSettings DefaultSettingsWithOverrides() => new()
    {
        Default = new LabelingThreshold { TargetPercent = 1.6, StopPercent = 0.4 },
        Overrides = new List<LabelingOverride>
        {
            new() { PairName = "BTCUSDT", Timeframe = "5m",  TargetPercent = 1.0, StopPercent = 0.3 },
            new() { PairName = "BTCUSDT", Timeframe = "15m", TargetPercent = 1.6, StopPercent = 0.4 }
        }
    };

    [Fact]
    public void GetThreshold_MatchingOverride_ReturnsOverrideValues()
    {
        var provider = BuildProvider(DefaultSettingsWithOverrides());

        var result = provider.GetThreshold("BTCUSDT", "5m");

        Assert.Equal(1.0, result.TargetPercent);
        Assert.Equal(0.3, result.StopPercent);
    }

    [Fact]
    public void GetThreshold_NoMatchingOverride_ReturnsDefault()
    {
        var provider = BuildProvider(DefaultSettingsWithOverrides());

        var result = provider.GetThreshold("ETHUSDT", "1h");

        Assert.Equal(1.6, result.TargetPercent);
        Assert.Equal(0.4, result.StopPercent);
    }

    [Fact]
    public void GetThreshold_CaseInsensitivePairMatch()
    {
        var provider = BuildProvider(DefaultSettingsWithOverrides());

        var result = provider.GetThreshold("btcusdt", "5m");

        Assert.Equal(1.0, result.TargetPercent);
    }

    [Fact]
    public void GetThreshold_CaseInsensitiveTimeframeMatch()
    {
        var provider = BuildProvider(DefaultSettingsWithOverrides());

        var result = provider.GetThreshold("BTCUSDT", "5M");

        Assert.Equal(1.0, result.TargetPercent);
    }

    [Fact]
    public void GetThreshold_EmptyOverrides_AlwaysReturnsDefault()
    {
        var provider = BuildProvider(new LabelingSettings
        {
            Default = new LabelingThreshold { TargetPercent = 2.0, StopPercent = 1.0 },
            Overrides = new List<LabelingOverride>()
        });

        var result = provider.GetThreshold("BTCUSDT", "5m");

        Assert.Equal(2.0, result.TargetPercent);
        Assert.Equal(1.0, result.StopPercent);
    }
}

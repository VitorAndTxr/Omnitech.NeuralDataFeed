using Omnitech.NeuralDataFeed.Domain.Enumerators;

namespace Omnitech.NeuralDataFeed.Tests;

public class CandleStickIntervalTests
{
    [Theory]
    [InlineData("1m",  1, "1m")]
    [InlineData("5m",  2, "5m")]
    [InlineData("15m", 3, "15m")]
    [InlineData("1h",  4, "1h")]
    public void FromCode_KnownCodes_ReturnsCorrectInterval(string code, int expectedId, string expectedCode)
    {
        var interval = CandleStickInterval.FromCode(code);

        Assert.Equal(expectedId, interval.Id);
        Assert.Equal(expectedCode, interval.Code);
    }

    [Fact]
    public void FromCode_UnknownCode_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CandleStickInterval.FromCode("4h"));
    }

    [Fact]
    public void FromCode_1m_ReturnsSameInstanceAsStaticField()
    {
        var result = CandleStickInterval.FromCode("1m");
        Assert.Equal(CandleStickInterval.OneMinuteInterval.Id, result.Id);
    }

    [Fact]
    public void FromCode_1h_ReturnsSameInstanceAsStaticField()
    {
        var result = CandleStickInterval.FromCode("1h");
        Assert.Equal(CandleStickInterval.OneHourInterval.Id, result.Id);
    }
}

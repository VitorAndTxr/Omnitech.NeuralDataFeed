using Omnitech.NeuralDataFeed.Domain.Enumerators;

namespace Omnitech.NeuralDataFeed.Domain.ExternalApi.Binance.Payloads
{
    public class GetCandlestickDataPayload
    {
        public string Symbol { get; set; }
        public CandleStickInterval Interval { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public int Limit { get; set; } = 1000;
    }
}

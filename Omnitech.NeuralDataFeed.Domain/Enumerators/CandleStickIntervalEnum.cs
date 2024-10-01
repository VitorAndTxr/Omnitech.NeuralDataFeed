using System.ComponentModel;

namespace Omnitech.NeuralDataFeed.Domain.Enumerators
{
    public class CandleStickInterval:Enumeration
    {
        public CandleStickInterval(int id, string code, string name) : base(id, code, name) { }

        public static CandleStickInterval OneMinuteInterval = new CandleStickInterval(1, "1m", "1m");

        public static CandleStickInterval FiveMinuteInterval = new CandleStickInterval(2, "5m", "5m");

        public static CandleStickInterval FifteenMinuteInterval = new CandleStickInterval(3, "15m", "15m");
    }
}

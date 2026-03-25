using System.ComponentModel;

namespace Omnitech.NeuralDataFeed.Domain.Enumerators
{
    public class CandleStickInterval:Enumeration
    {
        public CandleStickInterval(int id, string code, string name) : base(id, code, name) { }

        public static CandleStickInterval OneMinuteInterval = new CandleStickInterval(1, "1m", "1m");

        public static CandleStickInterval FiveMinuteInterval = new CandleStickInterval(2, "5m", "5m");

        public static CandleStickInterval FifteenMinuteInterval = new CandleStickInterval(3, "15m", "15m");

        public static CandleStickInterval OneHourInterval = new CandleStickInterval(4, "1h", "1h");

        public static CandleStickInterval FromCode(string code) => code switch
        {
            "1m"  => OneMinuteInterval,
            "5m"  => FiveMinuteInterval,
            "15m" => FifteenMinuteInterval,
            "1h"  => OneHourInterval,
            _     => throw new ArgumentException($"Unknown interval code: {code}")
        };
    }
}

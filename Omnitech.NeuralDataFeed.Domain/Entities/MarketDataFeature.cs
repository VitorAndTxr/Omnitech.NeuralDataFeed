namespace Omnitech.NeuralDataFeed.Domain.Entities
{
    public class MarketDataFeature
    {
        public string PairName { get; set; }
        public DateTime CandleOpenTime { get; set; }
        public string Timeframe { get; set; }
        // OHLCV
        public double OpenPrice { get; set; }
        public double HighPrice { get; set; }
        public double LowPrice { get; set; }
        public double ClosePrice { get; set; }
        public double Volume { get; set; }
        // Momentum
        public double? Rsi14 { get; set; }
        public double? Rsi7 { get; set; }
        public double? StochRsiK { get; set; }
        public double? StochRsiD { get; set; }
        public double? Roc14 { get; set; }
        // Trend
        public double? Ema9 { get; set; }
        public double? Ema21 { get; set; }
        public double? Ema50 { get; set; }
        public double? Ema200 { get; set; }
        public double? MacdLine { get; set; }
        public double? MacdSignal { get; set; }
        public double? MacdHistogram { get; set; }
        public double? Adx14 { get; set; }
        // Volatility
        public double? BbUpper { get; set; }
        public double? BbMiddle { get; set; }
        public double? BbLower { get; set; }
        public double? BbPctb { get; set; }
        public double? Atr14 { get; set; }
        // Volume
        public double? Obv { get; set; }
        public double? Vwap { get; set; }
        public double? VolumeSma20 { get; set; }
        public double? Cmf20 { get; set; }
        // Custom
        public double? PriceEma9Ratio { get; set; }
        public double? PriceEma21Ratio { get; set; }
        public double? MacdHistSlope { get; set; }
        // Support/Resistance
        public double? NearestSupport { get; set; }
        public double? NearestResistance { get; set; }
        public double? DistSupportPct { get; set; }
        public double? DistResistancePct { get; set; }
        public int? SupportStrength { get; set; }
        public int? ResistanceStrength { get; set; }
        public double? SrZonePosition { get; set; }
        public int? NumSrWithin1Pct { get; set; }
        // Labels
        public bool? BuySignal { get; set; }
        public bool? SellSignal { get; set; }
        public double? TargetPct { get; set; }
        public double? DrawdownPct { get; set; }
        // Meta
        public DateTime InsertedAt { get; set; }
    }
}

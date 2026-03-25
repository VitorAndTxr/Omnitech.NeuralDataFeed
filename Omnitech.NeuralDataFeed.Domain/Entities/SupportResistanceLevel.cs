namespace Omnitech.NeuralDataFeed.Domain.Entities
{
    public class SupportResistanceLevel
    {
        public int Id { get; set; }
        public string PairName { get; set; }
        public double PriceLevel { get; set; }
        public string LevelType { get; set; }   // "support" | "resistance"
        public int Strength { get; set; }
        public DateTime FirstDetectedAt { get; set; }
        public DateTime LastTestedAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

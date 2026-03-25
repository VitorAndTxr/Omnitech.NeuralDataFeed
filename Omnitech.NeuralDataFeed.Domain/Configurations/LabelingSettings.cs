namespace Omnitech.NeuralDataFeed.Domain.Configurations
{
    public class LabelingSettings
    {
        public LabelingThreshold Default { get; set; } = new();
        public List<LabelingOverride> Overrides { get; set; } = new();
    }

    public class LabelingThreshold
    {
        public double TargetPercent { get; set; } = 1.6;
        public double StopPercent { get; set; } = 0.4;
    }

    public class LabelingOverride
    {
        public string PairName { get; set; }
        public string Timeframe { get; set; }
        public double TargetPercent { get; set; }
        public double StopPercent { get; set; }
    }
}

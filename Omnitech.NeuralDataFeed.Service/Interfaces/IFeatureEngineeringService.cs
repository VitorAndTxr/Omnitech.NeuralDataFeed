namespace Omnitech.NeuralDataFeed.Service.Interfaces
{
    public interface IFeatureEngineeringService
    {
        Task CalculateFeaturesAsync(string pairName, string timeframe);
    }
}

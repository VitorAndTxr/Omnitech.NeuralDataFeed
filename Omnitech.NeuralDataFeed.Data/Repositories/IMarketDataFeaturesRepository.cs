using Omnitech.NeuralDataFeed.Domain.Entities;

namespace Omnitech.NeuralDataFeed.Data.Repositories
{
    public interface IMarketDataFeaturesRepository
    {
        Task BulkUpsertAsync(IEnumerable<MarketDataFeature> features);
        Task<List<MarketDataFeature>> GetUnlabeledAsync(string pairName, string timeframe, int batchSize);
        Task UpdateLabelsAsync(IEnumerable<MarketDataFeature> features);
        Task<List<MarketDataFeature>> GetFeaturesAsync(string pairName, string timeframe, DateTime from, DateTime to);
    }
}

using Omnitech.NeuralDataFeed.Domain.Entities;

namespace Omnitech.NeuralDataFeed.Data.Repositories
{
    public interface IMarketDataRepository
    {
        Task InsertMarketDataAsync(MarketData data);
        Task<List<MarketData>> GetMarketDataAsync(string pairName, DateTime startTime, DateTime endTime);
        Task<MarketData?> GetMostRecentCandleFromPairAsync(string pairName);
    }
}

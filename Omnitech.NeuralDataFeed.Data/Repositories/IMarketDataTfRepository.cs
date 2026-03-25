using Omnitech.NeuralDataFeed.Domain.Entities;

namespace Omnitech.NeuralDataFeed.Data.Repositories
{
    public interface IMarketDataTfRepository
    {
        Task BulkInsertAsync(IEnumerable<MarketDataTf> candles);
        Task<MarketDataTf?> GetMostRecentCandleAsync(string pairName, string timeframe);
        Task<List<MarketDataTf>> GetCandlesAsync(string pairName, string timeframe, DateTime from, DateTime to);
        Task<List<MarketDataTf>> GetLastNCandlesAsync(string pairName, string timeframe, int n);
    }
}

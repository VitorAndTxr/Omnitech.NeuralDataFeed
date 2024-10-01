using Omnitech.NeuralDataFeed.Domain.Entities;
using System.Threading.Tasks;

namespace Omnitech.NeuralDataFeed.Data.Repositories
{
    public interface IMarketDataRepository
    {
        Task InsertMarketDataAsync(MarketData data);
        Task InsertMarketDataListAsync(List<MarketData> dataList);
        Task<List<MarketData>> GetMarketDataAsync(string pairName, DateTime startTime, DateTime endTime);
        Task<MarketData?> GetMostRecentCandleFromPairAsync(string pairName);
        Task<List<MarketData>> GetFirstNCandleWithoutBuySignal(string pairName, int n);
        Task<MarketData?> GetFirstCandleWithoutSellSignal(string pairName);
        Task<int> CountCandlesticksWithoutBuySignal(string pairName);
        Task<List<MarketData>> GetNCandlestickStartingAtDateTime(string pairName, DateTime dateTime, int n);
        Task UpdateMarketBuyListAsync(IEnumerable<MarketData> dataList);
    }
}

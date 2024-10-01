using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Domain.ExternalApi.Binance.Payloads;

namespace Omnitech.NeuralDataFeed.Service.Interfaces
{
    public interface IBinanceService
    {
        Task<DateTime?> GetServerTime();
        Task<List<MarketData>> GetCandlestickData(GetCandlestickDataPayload payload);
    }
}

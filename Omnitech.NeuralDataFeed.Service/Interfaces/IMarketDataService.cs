using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Domain.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Omnitech.NeuralDataFeed.Service.Interfaces
{
    public interface IMarketDataService
    {
        Task UpdateMarketDataAsync();
        Task<List<MarketData>> GetMarketDataAsync(NeuralDataFeedRequestPayload payload);
    }
}

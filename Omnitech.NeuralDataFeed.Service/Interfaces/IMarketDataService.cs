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
    }
}

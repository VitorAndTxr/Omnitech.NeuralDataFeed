using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Omnitech.NeuralDataFeed.Domain.Configurations
{
    public class TradingPairSettings
    {
        public string Name { get; set; }
        public long FirstCandleUnixTimeMilliseconds { get; set; }
    }
}

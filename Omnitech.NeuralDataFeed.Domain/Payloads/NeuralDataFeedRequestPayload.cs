using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Omnitech.NeuralDataFeed.Domain.Payloads
{
    public class NeuralDataFeedRequestPayload
    {
        public required string PairName { get; set; }
        public int NumberOfCandles { get; set; }
        public DateTime? StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }

    }
}

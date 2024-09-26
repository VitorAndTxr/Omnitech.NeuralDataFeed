using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Omnitech.NeuralDataFeed.Domain.Entities
{
    public class MarketData
    {
        public int Id { get; set; }
        public string PairName { get; set; }
        public double OpenPrice { get; set; }
        public double ClosePrice { get; set; }
        public double HighPrice { get; set; }
        public double LowPrice { get; set; }
        public double Volume { get; set; }
        public DateTime CandleOpenTime { get; set; }
        public DateTime CandleCloseTime { get; set; }
        public bool BuySignal { get; set; }
        public bool SellSignal { get; set; }
        public string SequenceLabel { get; set; }
        public DateTime InsertedAt { get; set; }
    }
}

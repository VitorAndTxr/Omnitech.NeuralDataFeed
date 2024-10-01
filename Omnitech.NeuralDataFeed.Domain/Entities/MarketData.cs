using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Omnitech.NeuralDataFeed.Domain.Entities
{
    public class MarketData
    {
        [Column("pair_name")]
        public string PairName { get; set; }

        [Column("open_price")]
        public double OpenPrice { get; set; }

        [Column("close_price")]
        public double ClosePrice { get; set; }

        [Column("high_price")]
        public double HighPrice { get; set; }

        [Column("low_price")]
        public double LowPrice { get; set; }

        [Column("volume")]
        public double Volume { get; set; }

        [Column("candle_open_time")]
        public DateTime CandleOpenTime { get; set; }

        [Column("candle_close_time")]
        public DateTime CandleCloseTime { get; set; }

        [Column("buy_signal")]
        public bool? BuySignal { get; set; }

        [Column("sell_signal")]
        public bool? SellSignal { get; set; }

        [Column("sequence_label")]
        public string? SequenceLabel { get; set; }

        [Column("inserted_at")]
        public DateTime InsertedAt { get; set; }
    }
}

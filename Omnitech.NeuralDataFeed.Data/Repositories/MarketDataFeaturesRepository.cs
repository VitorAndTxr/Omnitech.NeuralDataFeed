using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Provider.Configurations;

namespace Omnitech.NeuralDataFeed.Data.Repositories
{
    public class MarketDataFeaturesRepository : IMarketDataFeaturesRepository
    {
        private readonly string _connectionString;

        public MarketDataFeaturesRepository(IOptions<DatabaseSettings> databaseSettings)
        {
            _connectionString = databaseSettings.Value.MarketDataDatabase;
        }

        public async Task BulkUpsertAsync(IEnumerable<MarketDataFeature> features)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                const string createTemp = @"
                    CREATE TEMP TABLE temp_features AS
                    SELECT * FROM market_data_features LIMIT 0;";
                await connection.ExecuteAsync(createTemp, transaction: transaction);

                using var writer = connection.BeginBinaryImport(@"
                    COPY temp_features (
                        pair_name, candle_open_time, timeframe,
                        open_price, high_price, low_price, close_price, volume,
                        rsi_14, rsi_7, stoch_rsi_k, stoch_rsi_d, roc_14,
                        ema_9, ema_21, ema_50, ema_200,
                        macd_line, macd_signal, macd_histogram, adx_14,
                        bb_upper, bb_middle, bb_lower, bb_pctb, atr_14,
                        obv, vwap, volume_sma_20, cmf_20,
                        price_ema9_ratio, price_ema21_ratio, macd_hist_slope,
                        nearest_support, nearest_resistance,
                        dist_support_pct, dist_resistance_pct,
                        support_strength, resistance_strength,
                        sr_zone_position, num_sr_within_1pct
                    ) FROM STDIN (FORMAT BINARY)");

                foreach (var f in features)
                {
                    await writer.StartRowAsync();
                    await writer.WriteAsync(f.PairName, NpgsqlDbType.Text);
                    await writer.WriteAsync(f.CandleOpenTime, NpgsqlDbType.TimestampTz);
                    await writer.WriteAsync(f.Timeframe, NpgsqlDbType.Text);
                    await writer.WriteAsync((decimal)f.OpenPrice, NpgsqlDbType.Numeric);
                    await writer.WriteAsync((decimal)f.HighPrice, NpgsqlDbType.Numeric);
                    await writer.WriteAsync((decimal)f.LowPrice, NpgsqlDbType.Numeric);
                    await writer.WriteAsync((decimal)f.ClosePrice, NpgsqlDbType.Numeric);
                    await writer.WriteAsync((decimal)f.Volume, NpgsqlDbType.Numeric);
                    await WriteNullableDouble(writer, f.Rsi14);
                    await WriteNullableDouble(writer, f.Rsi7);
                    await WriteNullableDouble(writer, f.StochRsiK);
                    await WriteNullableDouble(writer, f.StochRsiD);
                    await WriteNullableDouble(writer, f.Roc14);
                    await WriteNullableDouble(writer, f.Ema9);
                    await WriteNullableDouble(writer, f.Ema21);
                    await WriteNullableDouble(writer, f.Ema50);
                    await WriteNullableDouble(writer, f.Ema200);
                    await WriteNullableDouble(writer, f.MacdLine);
                    await WriteNullableDouble(writer, f.MacdSignal);
                    await WriteNullableDouble(writer, f.MacdHistogram);
                    await WriteNullableDouble(writer, f.Adx14);
                    await WriteNullableDouble(writer, f.BbUpper);
                    await WriteNullableDouble(writer, f.BbMiddle);
                    await WriteNullableDouble(writer, f.BbLower);
                    await WriteNullableDouble(writer, f.BbPctb);
                    await WriteNullableDouble(writer, f.Atr14);
                    await WriteNullableDouble(writer, f.Obv);
                    await WriteNullableDouble(writer, f.Vwap);
                    await WriteNullableDouble(writer, f.VolumeSma20);
                    await WriteNullableDouble(writer, f.Cmf20);
                    await WriteNullableDouble(writer, f.PriceEma9Ratio);
                    await WriteNullableDouble(writer, f.PriceEma21Ratio);
                    await WriteNullableDouble(writer, f.MacdHistSlope);
                    await WriteNullableDouble(writer, f.NearestSupport);
                    await WriteNullableDouble(writer, f.NearestResistance);
                    await WriteNullableDouble(writer, f.DistSupportPct);
                    await WriteNullableDouble(writer, f.DistResistancePct);
                    await WriteNullableInt(writer, f.SupportStrength);
                    await WriteNullableInt(writer, f.ResistanceStrength);
                    await WriteNullableDouble(writer, f.SrZonePosition);
                    await WriteNullableInt(writer, f.NumSrWithin1Pct);
                }

                await writer.CompleteAsync();

                const string upsert = @"
                    INSERT INTO market_data_features
                    SELECT * FROM temp_features
                    ON CONFLICT (pair_name, candle_open_time, timeframe)
                    DO UPDATE SET
                        open_price = EXCLUDED.open_price,
                        high_price = EXCLUDED.high_price,
                        low_price = EXCLUDED.low_price,
                        close_price = EXCLUDED.close_price,
                        volume = EXCLUDED.volume,
                        rsi_14 = EXCLUDED.rsi_14,
                        rsi_7 = EXCLUDED.rsi_7,
                        stoch_rsi_k = EXCLUDED.stoch_rsi_k,
                        stoch_rsi_d = EXCLUDED.stoch_rsi_d,
                        roc_14 = EXCLUDED.roc_14,
                        ema_9 = EXCLUDED.ema_9,
                        ema_21 = EXCLUDED.ema_21,
                        ema_50 = EXCLUDED.ema_50,
                        ema_200 = EXCLUDED.ema_200,
                        macd_line = EXCLUDED.macd_line,
                        macd_signal = EXCLUDED.macd_signal,
                        macd_histogram = EXCLUDED.macd_histogram,
                        adx_14 = EXCLUDED.adx_14,
                        bb_upper = EXCLUDED.bb_upper,
                        bb_middle = EXCLUDED.bb_middle,
                        bb_lower = EXCLUDED.bb_lower,
                        bb_pctb = EXCLUDED.bb_pctb,
                        atr_14 = EXCLUDED.atr_14,
                        obv = EXCLUDED.obv,
                        vwap = EXCLUDED.vwap,
                        volume_sma_20 = EXCLUDED.volume_sma_20,
                        cmf_20 = EXCLUDED.cmf_20,
                        price_ema9_ratio = EXCLUDED.price_ema9_ratio,
                        price_ema21_ratio = EXCLUDED.price_ema21_ratio,
                        macd_hist_slope = EXCLUDED.macd_hist_slope,
                        nearest_support = EXCLUDED.nearest_support,
                        nearest_resistance = EXCLUDED.nearest_resistance,
                        dist_support_pct = EXCLUDED.dist_support_pct,
                        dist_resistance_pct = EXCLUDED.dist_resistance_pct,
                        support_strength = EXCLUDED.support_strength,
                        resistance_strength = EXCLUDED.resistance_strength,
                        sr_zone_position = EXCLUDED.sr_zone_position,
                        num_sr_within_1pct = EXCLUDED.num_sr_within_1pct;";

                await connection.ExecuteAsync(upsert, transaction: transaction);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<MarketDataFeature>> GetUnlabeledAsync(string pairName, string timeframe, int batchSize)
        {
            const string sql = @"
                SELECT * FROM market_data_features
                WHERE pair_name = @PairName AND timeframe = @Timeframe
                  AND buy_signal IS NULL
                ORDER BY candle_open_time ASC
                LIMIT @BatchSize";

            using var connection = new NpgsqlConnection(_connectionString);
            var result = await connection.QueryAsync<MarketDataFeature>(sql,
                new { PairName = pairName, Timeframe = timeframe, BatchSize = batchSize });
            return result.ToList();
        }

        public async Task UpdateLabelsAsync(IEnumerable<MarketDataFeature> features)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                const string createTemp = @"
                    CREATE TEMP TABLE temp_labels (
                        pair_name TEXT,
                        candle_open_time TIMESTAMPTZ,
                        timeframe TEXT,
                        buy_signal BOOLEAN,
                        sell_signal BOOLEAN,
                        target_pct DOUBLE PRECISION,
                        drawdown_pct DOUBLE PRECISION
                    ) ON COMMIT DROP;";
                await connection.ExecuteAsync(createTemp, transaction: transaction);

                using var writer = connection.BeginBinaryImport(
                    "COPY temp_labels (pair_name, candle_open_time, timeframe, buy_signal, sell_signal, target_pct, drawdown_pct) FROM STDIN (FORMAT BINARY)");

                foreach (var f in features)
                {
                    await writer.StartRowAsync();
                    await writer.WriteAsync(f.PairName, NpgsqlDbType.Text);
                    await writer.WriteAsync(f.CandleOpenTime, NpgsqlDbType.TimestampTz);
                    await writer.WriteAsync(f.Timeframe, NpgsqlDbType.Text);
                    await WriteNullableBool(writer, f.BuySignal);
                    await WriteNullableBool(writer, f.SellSignal);
                    await WriteNullableDouble(writer, f.TargetPct);
                    await WriteNullableDouble(writer, f.DrawdownPct);
                }
                await writer.CompleteAsync();

                const string update = @"
                    UPDATE market_data_features mdf
                    SET buy_signal   = t.buy_signal,
                        sell_signal  = t.sell_signal,
                        target_pct   = t.target_pct,
                        drawdown_pct = t.drawdown_pct
                    FROM temp_labels t
                    WHERE mdf.pair_name = t.pair_name
                      AND mdf.candle_open_time = t.candle_open_time
                      AND mdf.timeframe = t.timeframe;";

                await connection.ExecuteAsync(update, transaction: transaction);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<MarketDataFeature>> GetFeaturesAsync(string pairName, string timeframe, DateTime from, DateTime to)
        {
            const string sql = @"
                SELECT * FROM market_data_features
                WHERE pair_name = @PairName AND timeframe = @Timeframe
                  AND candle_open_time >= @From AND candle_open_time <= @To
                ORDER BY candle_open_time ASC";

            using var connection = new NpgsqlConnection(_connectionString);
            var result = await connection.QueryAsync<MarketDataFeature>(sql,
                new { PairName = pairName, Timeframe = timeframe, From = from, To = to });
            return result.ToList();
        }

        private static async Task WriteNullableDouble(NpgsqlBinaryImporter writer, double? value)
        {
            if (value.HasValue)
                await writer.WriteAsync((decimal)value.Value, NpgsqlDbType.Numeric);
            else
                await writer.WriteNullAsync();
        }

        private static async Task WriteNullableInt(NpgsqlBinaryImporter writer, int? value)
        {
            if (value.HasValue)
                await writer.WriteAsync(value.Value, NpgsqlDbType.Integer);
            else
                await writer.WriteNullAsync();
        }

        private static async Task WriteNullableBool(NpgsqlBinaryImporter writer, bool? value)
        {
            if (value.HasValue)
                await writer.WriteAsync(value.Value, NpgsqlDbType.Boolean);
            else
                await writer.WriteNullAsync();
        }
    }
}

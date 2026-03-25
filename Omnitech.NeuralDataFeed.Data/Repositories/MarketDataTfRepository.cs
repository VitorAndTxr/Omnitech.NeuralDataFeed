using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Provider.Configurations;

namespace Omnitech.NeuralDataFeed.Data.Repositories
{
    public class MarketDataTfRepository : IMarketDataTfRepository
    {
        private readonly string _connectionString;

        public MarketDataTfRepository(IOptions<DatabaseSettings> databaseSettings)
        {
            _connectionString = databaseSettings.Value.MarketDataDatabase;
        }

        public async Task BulkInsertAsync(IEnumerable<MarketDataTf> candles)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using var writer = connection.BeginBinaryImport(
                "COPY market_data_tf (pair_name, candle_open_time, timeframe, open_price, high_price, low_price, close_price, volume, candle_close_time) FROM STDIN (FORMAT BINARY)");

            foreach (var c in candles)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(c.PairName, NpgsqlDbType.Text);
                await writer.WriteAsync(c.CandleOpenTime, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(c.Timeframe, NpgsqlDbType.Text);
                await writer.WriteAsync(c.OpenPrice, NpgsqlDbType.Double);
                await writer.WriteAsync(c.HighPrice, NpgsqlDbType.Double);
                await writer.WriteAsync(c.LowPrice, NpgsqlDbType.Double);
                await writer.WriteAsync(c.ClosePrice, NpgsqlDbType.Double);
                await writer.WriteAsync(c.Volume, NpgsqlDbType.Double);
                await writer.WriteAsync(c.CandleCloseTime, NpgsqlDbType.TimestampTz);
            }

            await writer.CompleteAsync();
        }

        public async Task<MarketDataTf?> GetMostRecentCandleAsync(string pairName, string timeframe)
        {
            const string sql = @"
                SELECT * FROM market_data_tf
                WHERE pair_name = @PairName AND timeframe = @Timeframe
                ORDER BY candle_open_time DESC
                LIMIT 1";

            using var connection = new NpgsqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<MarketDataTf>(sql, new { PairName = pairName, Timeframe = timeframe });
        }

        public async Task<List<MarketDataTf>> GetCandlesAsync(string pairName, string timeframe, DateTime from, DateTime to)
        {
            const string sql = @"
                SELECT * FROM market_data_tf
                WHERE pair_name = @PairName AND timeframe = @Timeframe
                  AND candle_open_time >= @From AND candle_open_time <= @To
                ORDER BY candle_open_time ASC";

            using var connection = new NpgsqlConnection(_connectionString);
            var result = await connection.QueryAsync<MarketDataTf>(sql, new { PairName = pairName, Timeframe = timeframe, From = from, To = to });
            return result.ToList();
        }

        public async Task<List<MarketDataTf>> GetLastNCandlesAsync(string pairName, string timeframe, int n)
        {
            const string sql = @"
                SELECT * FROM (
                    SELECT * FROM market_data_tf
                    WHERE pair_name = @PairName AND timeframe = @Timeframe
                    ORDER BY candle_open_time DESC
                    LIMIT @N
                ) sub
                ORDER BY candle_open_time ASC";

            using var connection = new NpgsqlConnection(_connectionString);
            var result = await connection.QueryAsync<MarketDataTf>(sql, new { PairName = pairName, Timeframe = timeframe, N = n });
            return result.ToList();
        }
    }
}

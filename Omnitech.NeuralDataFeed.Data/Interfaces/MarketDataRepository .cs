using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Provider.Configurations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Omnitech.NeuralDataFeed.Data.Interfaces
{
    public class MarketDataRepository : IMarketDataRepository
    {
        private readonly string _connectionString;

        public MarketDataRepository(IOptions<DatabaseSettings> databaseSettings)
        {
            _connectionString = databaseSettings.Value.MarketDataDatabase;
        }

        public async Task InsertMarketDataAsync(MarketData data)
        {
            var query = @"INSERT INTO market_data 
                          (pair_name, open_price, close_price, high_price, low_price, volume, candle_open_time, candle_close_time, sequence_label)
                          VALUES 
                          (@PairName, @OpenPrice, @ClosePrice, @HighPrice, @LowPrice, @Volume, @CandleOpenTime, @CandleCloseTime, @SequenceLabel)";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(query, data);
        }
        public async Task InsertMarketDataListAsync(List<MarketData> dataList)
        {
            var query = @"INSERT INTO market_data 
                          (pair_name, open_price, close_price, high_price, low_price, volume, candle_open_time, candle_close_time, sequence_label)
                          VALUES 
                          (@PairName, @OpenPrice, @ClosePrice, @HighPrice, @LowPrice, @Volume, @CandleOpenTime, @CandleCloseTime,  @SequenceLabel)";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(query, dataList);
        }

        public async Task<List<MarketData>> GetMarketDataAsync(string pairName, DateTime startTime, DateTime endTime)
        {
            var query = @"SELECT * FROM market_data
                          WHERE pair_name = @PairName
                          AND candle_close_time BETWEEN @StartTime AND @EndTime
                          ORDER BY candle_close_time ASC";

            using var connection = new NpgsqlConnection(_connectionString);

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("PairName", pairName);
            parameters.Add("StartTime", startTime);
            parameters.Add("EndTime", endTime);

            var result = await connection.QueryAsync<MarketData>(query, parameters);
            return result.AsList();
        }

        public async Task<MarketData?> GetMostRecentCandleFromPairAsync(string pairName)
        {
            var query = @"SELECT * FROM market_data
                          WHERE pair_name = @PairName
                          ORDER BY candle_close_time DESC
                          LIMIT 1";

            using var connection = new NpgsqlConnection(_connectionString);

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("PairName", pairName);

            var result = await connection.QueryFirstOrDefaultAsync<MarketData>(query, parameters);
            return result;
        }

        public async Task<List<MarketData>> GetFirstNCandleWithoutBuySignal(string pairName, int n)
        {
            var query = @"SELECT * FROM market_data
                          WHERE pair_name = @PairName
                          AND buy_signal IS NULL
                          ORDER BY candle_close_time ASC
                          LIMIT @N";

            using var connection = new NpgsqlConnection(_connectionString);

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("PairName", pairName);
            parameters.Add("N", n);

            var result = await connection.QueryAsync<MarketData>(query, parameters);
            return result.ToList();
        }

        public async Task<MarketData?> GetFirstCandleWithoutSellSignal(string pairName)
        {
            var query = @"SELECT top 1 FROM market_data
                          WHERE pair_name = @PairName
                          AND sell_signal IS NULL
                          ORDER BY candle_close_time ASC";

            using var connection = new NpgsqlConnection(_connectionString);

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("PairName", pairName);

            var result = await connection.QueryAsync<MarketData>(query, parameters);
            return result.FirstOrDefault();
        }

        public async Task<List<MarketData>> GetNCandlestickStartingAtDateTime(string pairName, DateTime dateTime, int n)
        {
            var query = @"SELECT * @number FROM market_data
                          WHERE pair_name = @PairName
                          AND candle_close_time >= @DateTime
                          ORDER BY candle_close_time
                          LIMIT @N";

            using var connection = new NpgsqlConnection(_connectionString);

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("PairName", pairName);
            parameters.Add("DateTime", dateTime);
            parameters.Add("N", n);

            var result = await connection.QueryAsync<MarketData>(query, parameters);
            return result.AsList();
        }

        public async Task<int> CountCandlesticksWithoutBuySignal(string pairName)
        {
            var query = @"SELECT COUNT(*) FROM market_data
                          WHERE pair_name = @PairName
                          AND buy_signal IS NULL";

            using var connection = new NpgsqlConnection(_connectionString);

            DynamicParameters parameters = new DynamicParameters();
            parameters.Add("PairName", pairName);

            var result = await connection.QueryFirstOrDefaultAsync<int>(query, parameters);
            return result;
        }

        public async Task UpdateMarketBuyListAsync(IEnumerable<MarketData> dataList)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                // Cria uma tabela temporária
                var createTempTableSql = @"
            CREATE TEMP TABLE temp_market_data (
                pair_name text,
                candle_open_time timestamp with time zone,
                buy_signal boolean
            ) ON COMMIT DROP;
        ";
                await connection.ExecuteAsync(createTempTableSql, transaction: transaction);

                // Usa NpgsqlBinaryImporter para inserir os dados em lote na tabela temporária
                using (var writer = connection.BeginBinaryImport("COPY temp_market_data (pair_name, candle_open_time, buy_signal) FROM STDIN (FORMAT BINARY)"))
                {
                    foreach (var data in dataList)
                    {
                        await writer.StartRowAsync();
                        await writer.WriteAsync(data.PairName, NpgsqlTypes.NpgsqlDbType.Text);
                        await writer.WriteAsync(data.CandleOpenTime, NpgsqlTypes.NpgsqlDbType.TimestampTz);
                        await writer.WriteAsync(data.BuySignal, NpgsqlTypes.NpgsqlDbType.Boolean);
                    }
                    await writer.CompleteAsync();
                }

                // Executa a atualização usando join com a tabela temporária
                var updateSql = @"
            UPDATE market_data
            SET buy_signal = t.buy_signal
            FROM temp_market_data t
            WHERE market_data.pair_name = t.pair_name
            AND market_data.candle_open_time = t.candle_open_time;
        ";
                await connection.ExecuteAsync(updateSql, transaction: transaction);

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}

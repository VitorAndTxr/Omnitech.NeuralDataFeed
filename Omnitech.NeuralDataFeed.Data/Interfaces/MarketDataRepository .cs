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
                          (pair_name, open_price, close_price, high_price, low_price, volume, candle_open_time, candle_close_time, buy_signal, sell_signal, sequence_label)
                          VALUES 
                          (@PairName, @OpenPrice, @ClosePrice, @HighPrice, @LowPrice, @Volume, @CandleOpenTime, @CandleCloseTime, @BuySignal, @SellSignal, @SequenceLabel)";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(query, data);
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
    }
}

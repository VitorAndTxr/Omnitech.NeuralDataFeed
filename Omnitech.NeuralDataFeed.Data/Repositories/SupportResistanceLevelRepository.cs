using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Provider.Configurations;

namespace Omnitech.NeuralDataFeed.Data.Repositories
{
    public class SupportResistanceLevelRepository : ISupportResistanceLevelRepository
    {
        private readonly string _connectionString;

        public SupportResistanceLevelRepository(IOptions<DatabaseSettings> databaseSettings)
        {
            _connectionString = databaseSettings.Value.MarketDataDatabase;
        }

        public async Task UpsertLevelsAsync(IEnumerable<SupportResistanceLevel> levels)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // Deactivate existing active levels for the pair
                var levelList = levels.ToList();
                if (!levelList.Any()) return;

                var pairName = levelList[0].PairName;

                // Insert or update levels. The natural identity of a level is
                // (pair_name, level_type, first_detected_at); update strength and
                // last_tested_at on conflict so re-runs reflect the latest cluster data.
                foreach (var level in levelList)
                {
                    const string upsertSql = @"
                        INSERT INTO support_resistance_levels
                            (pair_name, price_level, level_type, strength, first_detected_at, last_tested_at, is_active)
                        VALUES
                            (@PairName, @PriceLevel, @LevelType, @Strength, @FirstDetectedAt, @LastTestedAt, @IsActive)
                        ON CONFLICT (pair_name, level_type, first_detected_at)
                        DO UPDATE SET
                            price_level    = EXCLUDED.price_level,
                            strength       = EXCLUDED.strength,
                            last_tested_at = EXCLUDED.last_tested_at,
                            is_active      = EXCLUDED.is_active";

                    await connection.ExecuteAsync(upsertSql, new
                    {
                        level.PairName,
                        level.PriceLevel,
                        level.LevelType,
                        level.Strength,
                        level.FirstDetectedAt,
                        level.LastTestedAt,
                        level.IsActive
                    }, transaction);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<SupportResistanceLevel>> GetActiveLevelsAsync(string pairName)
        {
            const string sql = @"
                SELECT * FROM support_resistance_levels
                WHERE pair_name = @PairName AND is_active = TRUE
                ORDER BY price_level ASC";

            using var connection = new NpgsqlConnection(_connectionString);
            var result = await connection.QueryAsync<SupportResistanceLevel>(sql, new { PairName = pairName });
            return result.ToList();
        }

        public async Task<List<SupportResistanceLevel>> GetAllLevelsAsync(string pairName)
        {
            const string sql = @"
                SELECT * FROM support_resistance_levels
                WHERE pair_name = @PairName
                ORDER BY price_level ASC";

            using var connection = new NpgsqlConnection(_connectionString);
            var result = await connection.QueryAsync<SupportResistanceLevel>(sql, new { PairName = pairName });
            return result.ToList();
        }

        public async Task DeactivateStaleAsync(string pairName, DateTime cutoff)
        {
            const string sql = @"
                UPDATE support_resistance_levels
                SET is_active = FALSE
                WHERE pair_name = @PairName
                  AND is_active = TRUE
                  AND last_tested_at < @Cutoff";

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(sql, new { PairName = pairName, Cutoff = cutoff });
        }
    }
}
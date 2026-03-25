using Omnitech.NeuralDataFeed.Domain.Entities;

namespace Omnitech.NeuralDataFeed.Data.Repositories
{
    public interface ISupportResistanceLevelRepository
    {
        Task UpsertLevelsAsync(IEnumerable<SupportResistanceLevel> levels);
        Task<List<SupportResistanceLevel>> GetActiveLevelsAsync(string pairName);
        Task DeactivateStaleAsync(string pairName, DateTime cutoffTime);
    }
}

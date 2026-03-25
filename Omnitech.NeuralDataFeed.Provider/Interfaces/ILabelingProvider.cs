using Omnitech.NeuralDataFeed.Domain.Configurations;

namespace Omnitech.NeuralDataFeed.Provider.Interfaces
{
    public interface ILabelingProvider
    {
        LabelingThreshold GetThreshold(string pairName, string timeframe);
    }
}

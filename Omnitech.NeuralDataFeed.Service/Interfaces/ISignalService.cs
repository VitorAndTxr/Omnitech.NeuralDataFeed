namespace Omnitech.NeuralDataFeed.Service.Interfaces
{
    public interface ISignalService
    {
        Task UpdateBuySignalAsync();
        Task UpdateLabelsAsync(string pairName);
    }
}

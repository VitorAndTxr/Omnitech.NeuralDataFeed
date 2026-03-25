namespace Omnitech.NeuralDataFeed.Service.Interfaces
{
    public interface IBinanceWebSocketService
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync();
    }
}

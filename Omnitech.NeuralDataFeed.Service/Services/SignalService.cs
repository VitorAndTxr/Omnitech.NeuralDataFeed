using Microsoft.Extensions.Logging;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Provider.Interfaces;
using Omnitech.NeuralDataFeed.Provider.Providers;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using System.Reflection.Metadata;

namespace Omnitech.NeuralDataFeed.Service.Services
{
    public class SignalService : ISignalService
    {
        private readonly ILogger<SignalService> _logger;
        private readonly IMarketDataRepository _marketDataRepository;
        private readonly ITradingPairProvider _tradingPairProvider;


        public SignalService(
            ILogger<SignalService> logger,
            IMarketDataRepository marketDataRepository,
            ITradingPairProvider tradingPairProvider
            )
        {
            _marketDataRepository = marketDataRepository;
            _tradingPairProvider = tradingPairProvider;
            _logger = logger;
        }

        public async Task UpdateBuySignalAsync()
        {
            try
            {

                // Implementação da lógica de atualização do sinal de compra
                _logger.LogInformation("Iniciando a atualização dos sinais de compra...");
                var tradingPairs = _tradingPairProvider.GetTradingPairs();

                foreach (var tradingPair in tradingPairs)
                {
                    await UpdateTradingPairBuySignal(tradingPair);
                }


                _logger.LogInformation("Atualização dos sinais de compra concluída.");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar os dados de mercado.");
            }
        }

        private async Task UpdateTradingPairBuySignal(TradingPairSettings tradingPair)
        {
            try
            {
                int totalCandlesticksWithoutBuySignal = 0;

                do
                {
                    totalCandlesticksWithoutBuySignal = await _marketDataRepository.CountCandlesticksWithoutBuySignal(tradingPair.Name);

                    if (totalCandlesticksWithoutBuySignal < 100000)
                        break;

                    var candlesticksToVerify = await _marketDataRepository.GetFirstNCandleWithoutBuySignal(tradingPair.Name, 100000);

                    double target = 0.016;
                    double stop = 0.04;

                    foreach (var candle in candlesticksToVerify)
                    {
                        var index = candlesticksToVerify.IndexOf(candle);

                        for (var i = index + 1; i < candlesticksToVerify.Count; i++)
                        {
                            var actualCandle = candlesticksToVerify.ElementAt(i);

                            candle.BuySignal = null;
                            if (actualCandle.LowPrice < (1 - stop) * (candle.ClosePrice))
                            {
                                candle.BuySignal = false;
                                break;
                            }
                            else if (actualCandle.HighPrice > (1 + target) * (candle.ClosePrice))
                            {
                                candle.BuySignal = true;
                                break;
                            }

                        }

                    }

                    await _marketDataRepository.UpdateMarketBuyListAsync(candlesticksToVerify);

                }
                while (totalCandlesticksWithoutBuySignal > 100000);

                // Implementação da lógica de atualização do sinal de compra
                
            }
            catch
            {
                throw;
            }
        }
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Provider.Configurations;
using Omnitech.NeuralDataFeed.Provider.Interfaces;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using System.Text.Json;

namespace Omnitech.NeuralDataFeed.Service.Services
{
    public class MarketDataService : IMarketDataService
    {
        private readonly ILogger<MarketDataService> _logger;
        private readonly IMarketDataRepository _marketDataRepository;
        private readonly ITradingPairProvider _tradingPairProvider;
        private readonly string _connectionString;
        public MarketDataService(
            ILogger<MarketDataService> logger,
            IMarketDataRepository marketDataRepository,
            ITradingPairProvider tradingPairProvider,
            IOptions<DatabaseSettings> databaseSettings)
        {
            _logger = logger;
            _marketDataRepository = marketDataRepository;   
            _tradingPairProvider = tradingPairProvider;
            _connectionString = databaseSettings.Value.MarketDataDatabase;
        }
        public async Task UpdateMarketDataAsync()
        {
            try
            {
                // Lógica para atualizar os dados de mercado
                _logger.LogInformation("Iniciando a atualização dos dados de mercado...");

                var tradingPairs = _tradingPairProvider.GetTradingPairs();

                foreach(var tradingPair in tradingPairs)
                {
                    await UpdateTradingPairMarketData(tradingPair);
                }

                // Buscar pares de Moedas

                //while()

                _logger.LogInformation("Atualização dos dados de mercado concluída.");
                await Task.CompletedTask;
               
            }catch(Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar os dados de mercado.");
            }
        }

        public async Task UpdateTradingPairMarketData(TradingPairSettings tradingPair)
        {
            try
            {
                // Lógica para atualizar os dados de mercado
                _logger.LogInformation($"Iniciando a atualização dos dados de mercado para o par {tradingPair.Name}...");

                var lastCandleTimeStamp = DateTimeOffset.FromUnixTimeMilliseconds(tradingPair.FirstCandleUnixTimeMilliseconds).DateTime;

                var lastCandle = await _marketDataRepository.GetMostRecentCandleFromPairAsync(tradingPair.Name);

                if (lastCandle!=null)
                {
                    lastCandleTimeStamp = lastCandle.CandleCloseTime;
                }
                //var marketData = await _tradingPairProvider.GetMarketDataAsync(tradingPair);

                //return Ok(new { message = "Conexão com o banco de dados estabelecida com sucesso." });


                _logger.LogInformation($"Dados de mercado atualizados para o par {tradingPair.Name}");

                _logger.LogInformation("Atualização dos dados de mercado concluída.");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao atualizar os dados de mercado para o par {tradingPair}.");
            }
        }
    }
}

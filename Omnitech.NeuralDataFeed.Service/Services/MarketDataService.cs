﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Domain.Enumerators;
using Omnitech.NeuralDataFeed.Domain.ExternalApi.Binance.Payloads;
using Omnitech.NeuralDataFeed.Domain.Payloads;
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
        private readonly IBinanceService _binanceService;
        private readonly string _connectionString;
        public MarketDataService(
            ILogger<MarketDataService> logger,
            IMarketDataRepository marketDataRepository,
            ITradingPairProvider tradingPairProvider,
            IBinanceService binanceService,
            IOptions<DatabaseSettings> databaseSettings)
        {
            _logger = logger;
            _marketDataRepository = marketDataRepository;   
            _tradingPairProvider = tradingPairProvider;
            _connectionString = databaseSettings.Value.MarketDataDatabase;
            _binanceService = binanceService;   
        }
        public async Task UpdateMarketDataAsync()
        {
            try
            {
                // Lógica para atualizar os dados de mercado
                _logger.LogInformation("Iniciando a atualização dos dados de mercado...");

                var tradingPairs = _tradingPairProvider.GetTradingPairs();

                await _binanceService.GetServerTime();

                foreach(var tradingPair in tradingPairs)
                {
                    await UpdateTradingPairMarketData(tradingPair);
                }

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
                    lastCandleTimeStamp = lastCandle.CandleOpenTime;
                }
                do
                {

                    var candlestickData = await _binanceService.GetCandlestickData(new GetCandlestickDataPayload
                    {
                        Symbol = tradingPair.Name,
                        Interval = CandleStickInterval.OneMinuteInterval,
                        StartTime = (lastCandleTimeStamp.AddMinutes(1) - new DateTime(1970, 1, 1)).TotalMilliseconds
                    });

                    if(candlestickData.Count == 0)
                    {
                        break;
                    }

                    await _marketDataRepository.InsertMarketDataListAsync(candlestickData);

                    lastCandleTimeStamp = candlestickData.Last().CandleOpenTime;

                } while (lastCandleTimeStamp < DateTime.UtcNow.AddMinutes(-1));

                //var marketData = await _tradingPairProvider.GetMarketDataAsync(tradingPair);

                //return Ok(new { message = "Conexão com o banco de dados estabelecida com sucesso." });


                _logger.LogInformation($"Dados de mercado atualizados para o par {tradingPair.Name}");

                _logger.LogInformation("Atualização dos dados de mercado concluída.");
                
                await Task.CompletedTask;
            }
            catch
            {
                throw;
            }
        }

        public async Task<List<MarketData>> GetMarketDataAsync(NeuralDataFeedRequestPayload payload)
        {

            try
            {
                // Lógica para atualizar os dados de mercado
                _logger.LogInformation("Iniciando a busca dos dados de mercado...");

                if(payload.StartDateTime == null&&payload.EndDateTime == null)
                {
                    throw new Exception("É necessário informar a data de início ou a data de fim.");
                }

                if (payload.StartDateTime == null)
                {
                    payload.EndDateTime = payload.EndDateTime!.Value.AddMinutes(1);
                    payload.StartDateTime = payload.EndDateTime!.Value.AddMinutes(-payload.NumberOfCandles);
                }

                if (payload.EndDateTime == null)
                {
                    var startDateTime = payload.StartDateTime!.Value;
                    payload.EndDateTime = startDateTime.AddMinutes(payload.NumberOfCandles);
                }

                var marketData = await _marketDataRepository.GetMarketDataAsync(payload.PairName, payload.StartDateTime!.Value, payload.EndDateTime!.Value);
                
                return marketData;
            }
            catch
            {
                throw;
            }
        }
    }
}

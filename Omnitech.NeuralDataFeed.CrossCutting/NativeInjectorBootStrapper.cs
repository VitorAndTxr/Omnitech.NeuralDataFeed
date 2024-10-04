
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Omnitech.NeuralDataFeed.Data;
using Omnitech.NeuralDataFeed.Data.Interfaces;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Domain.Configurations;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Provider.Configurations;
using Omnitech.NeuralDataFeed.Provider.Interfaces;
using Omnitech.NeuralDataFeed.Provider.Providers;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using Omnitech.NeuralDataFeed.Service.Services;
using Omnitech.NeuralDataFeed.Service.Services.WebSockets;

namespace Omnitech.NeuralDataFeed.CrossCutting
{
    public class NativeInjectorBootStrapper
    {
        public static void RegisterWorkerDependencies(IServiceCollection services, IConfiguration configuration)
        {
            AddProviders(services, configuration);
            AddDatabase(services, configuration);
            AddServices(services);
            AddRepositories(services);

            Dapper.SqlMapper.SetTypeMap(typeof(MarketData), new SnakeCaseToCamelCaseMapper(typeof(MarketData)));

        }

        public static void RegisterHostDependencies(IServiceCollection services, IConfiguration configuration)
        {
            AddProviders(services, configuration);
            AddDatabase(services, configuration);
            AddServices(services);
            AddRepositories(services);
            AddWebSocket(services);

            Dapper.SqlMapper.SetTypeMap(typeof(MarketData), new SnakeCaseToCamelCaseMapper(typeof(MarketData)));

        }

        private static void AddProviders(IServiceCollection services, IConfiguration configuration)
        {
            var tradingPairSettings = configuration.GetSection("TradingPairs");

            if (tradingPairSettings== null)
            {
                throw new System.Exception("TradingPairs section not found in appsettings.json");
            }

            services.Configure<List<TradingPairSettings>>(tradingPairSettings);

            services.AddScoped<ITradingPairProvider, TradingPairProvider>();

        }
        private static void AddServices(IServiceCollection services)
        {
            services.AddScoped<IMarketDataService, MarketDataService>();
            services.AddScoped<ISignalService, SignalService>();
            services.AddScoped<IBinanceService, BinanceService>();

        }



        private static void AddRepositories(IServiceCollection services)
        {
            services.AddScoped<IMarketDataRepository, MarketDataRepository>();
        }

        private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
        {
            var dataBaseSetting = configuration.GetSection("ConnectionStrings");

            if (dataBaseSetting == null)
            {
                throw new System.Exception("ConnectionStrings section not found in appsettings.json");
            }

            services.Configure<DatabaseSettings>(dataBaseSetting);
        }

        private static void AddWebSocket(IServiceCollection services)
        {
            services.AddSingleton<WebSocketHandler>();
            services.AddSingleton<WebSocketConnectionManager>();

        }
    }
}

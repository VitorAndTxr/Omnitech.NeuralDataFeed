
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            RegisterTypeMaps();
        }

        public static void RegisterHostDependencies(IServiceCollection services, IConfiguration configuration)
        {
            AddProviders(services, configuration);
            AddDatabase(services, configuration);
            AddServices(services);
            AddRepositories(services);
            AddWebSocket(services);
            RegisterTypeMaps();
        }

        private static void AddProviders(IServiceCollection services, IConfiguration configuration)
        {
            var tradingPairSettings = configuration.GetSection("TradingPairs");

            if (tradingPairSettings == null)
                throw new Exception("TradingPairs section not found in appsettings.json");

            services.Configure<List<TradingPairSettings>>(tradingPairSettings);
            services.Configure<LabelingSettings>(configuration.GetSection("LabelingSettings"));

            services.AddScoped<ITradingPairProvider, TradingPairProvider>();
            services.AddScoped<ILabelingProvider, LabelingProvider>();
        }

        private static void AddServices(IServiceCollection services)
        {
            services.AddHttpClient<IBinanceService, BinanceService>(client =>
            {
                client.BaseAddress = new Uri("https://api.binance.com/api/v3/");
            });

            services.AddScoped<IMarketDataService, MarketDataService>();
            services.AddScoped<ISignalService, SignalService>();
            services.AddScoped<IFeatureEngineeringService, FeatureEngineeringService>();
            services.AddScoped<ISupportResistanceService, SupportResistanceService>();
            services.AddSingleton<IBinanceWebSocketService, BinanceWebSocketService>();
        }

        private static void AddRepositories(IServiceCollection services)
        {
            services.AddScoped<IMarketDataRepository, MarketDataRepository>();
            services.AddScoped<IMarketDataTfRepository, MarketDataTfRepository>();
            services.AddScoped<IMarketDataFeaturesRepository, MarketDataFeaturesRepository>();
            services.AddScoped<ISupportResistanceLevelRepository, SupportResistanceLevelRepository>();
        }

        private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
        {
            var dataBaseSetting = configuration.GetSection("ConnectionStrings");

            if (dataBaseSetting == null)
                throw new Exception("ConnectionStrings section not found in appsettings.json");

            services.Configure<DatabaseSettings>(dataBaseSetting);
        }

        private static void AddWebSocket(IServiceCollection services)
        {
            services.AddSingleton<WebSocketHandler>();
            services.AddSingleton<WebSocketConnectionManager>();
        }

        private static void RegisterTypeMaps()
        {
            Dapper.SqlMapper.SetTypeMap(typeof(MarketData),            new SnakeCaseToCamelCaseMapper(typeof(MarketData)));
            Dapper.SqlMapper.SetTypeMap(typeof(MarketDataTf),          new SnakeCaseToCamelCaseMapper(typeof(MarketDataTf)));
            Dapper.SqlMapper.SetTypeMap(typeof(MarketDataFeature),     new SnakeCaseToCamelCaseMapper(typeof(MarketDataFeature)));
            Dapper.SqlMapper.SetTypeMap(typeof(SupportResistanceLevel),new SnakeCaseToCamelCaseMapper(typeof(SupportResistanceLevel)));
        }
    }
}

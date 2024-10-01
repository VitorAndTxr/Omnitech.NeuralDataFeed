using Omnitech.NeuralDataFeed.CrossCutting;
using Omnitech.NeuralDataFeed.Data;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using Serilog;
using Serilog.Events;

namespace Omnitech.NeuralDataFeed.Worker
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Habilitar logging interno do Serilog para diagnóstico

            Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine(msg));

            // Construir a configuração
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Configurar o Serilog

            string logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Omnitech", "Logs");

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH:mm:ss");

            string logFileName = Path.Combine(logDirectory, $"exec_{timestamp}.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information) // Ajusta o nível mínimo para logs do sistema
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(
                    logFileName,
                    rollingInterval: RollingInterval.Infinite,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            // Executar a tarefa principal
            try
            {
                var serviceCollection = new ServiceCollection();
                Log.Warning("Iniciando aplicação");
                ConfigureServices(serviceCollection, configuration);

                // Construir provedor de serviços
                var serviceProvider = serviceCollection.BuildServiceProvider();

                var marketDataService = serviceProvider.GetRequiredService<IMarketDataService>();
                var signalService = serviceProvider.GetRequiredService<ISignalService>();

                await marketDataService.UpdateMarketDataAsync();
                await signalService.UpdateBuySignalAsync();

                Log.Information("Aplicação finalizada com sucesso.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Um erro ocorreu durante a execução da aplicação.");
            }
            finally
            {
                // Garantir que todos os logs sejam gravados antes de encerrar a aplicação
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Registrar serviços
            NativeInjectorBootStrapper.RegisterDependencies(services, configuration);
            // Configurar o logging para usar o Serilog
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(LogLevel.Debug);
                loggingBuilder.AddSerilog();
            });
        }
    }
}

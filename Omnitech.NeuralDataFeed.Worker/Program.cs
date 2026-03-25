using Omnitech.NeuralDataFeed.CrossCutting;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using Serilog;
using Serilog.Events;

namespace Omnitech.NeuralDataFeed.Worker
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Habilitar logging interno do Serilog para diagn�stico

            Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine(msg));

            // Construir a configura��o
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
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information) // Ajusta o n�vel m�nimo para logs do sistema
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
                Log.Warning("Iniciando aplica��o");
                ConfigureServices(serviceCollection, configuration);

                // Construir provedor de servi�os
                var serviceProvider = serviceCollection.BuildServiceProvider();

                var marketDataService = serviceProvider.GetRequiredService<IMarketDataService>();

                // UpdateMarketDataAsync orchestrates ingestion, S/R detection, feature engineering,
                // and labeling for all pairs internally — no additional signal service call needed.
                await marketDataService.UpdateMarketDataAsync();

                Log.Information("Aplica��o finalizada com sucesso.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Um erro ocorreu durante a execu��o da aplica��o.");
            }
            finally
            {
                // Garantir que todos os logs sejam gravados antes de encerrar a aplica��o
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Registrar servi�os
            NativeInjectorBootStrapper.RegisterWorkerDependencies(services, configuration);
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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Omnitech.NeuralDataFeed.Domain.Entities;
using Omnitech.NeuralDataFeed.Domain.Payloads;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Omnitech.NeuralDataFeed.Service.Services.WebSockets
{
    public class WebSocketHandler
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public WebSocketHandler( IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }
        public async Task HandleAsync(HttpContext context, WebSocket webSocket)
        {
            Console.WriteLine($"Socket Aberto");
            var buffer = new byte[1024 * 4];

            WebSocketReceiveResult result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);

            while (!result.CloseStatus.HasValue)
            {
                // Processar os dados recebidos
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"Mensagem recebida: {message}");

                if (message != null)
                {
                    NeuralDataFeedRequestPayload payload = JsonSerializer.Deserialize<NeuralDataFeedRequestPayload>(message);
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var marketDataService = scope.ServiceProvider.GetRequiredService<IMarketDataService>();

                        var marketData = await marketDataService.GetMarketDataAsync(payload);

                        // Opcional: Enviar uma resposta de volta ao cliente


                        string response = JsonSerializer.Serialize(marketData);
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(responseBytes, 0, responseBytes.Length),
                            result.MessageType,
                            result.EndOfMessage,
                            CancellationToken.None);
                    }

                    // Continuar recebendo dados
                    result = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), CancellationToken.None);
                }

            }

            await webSocket.CloseAsync(
                result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
            Console.WriteLine($"Socket Fechado");

        }

    }
}

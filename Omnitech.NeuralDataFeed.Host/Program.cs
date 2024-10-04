using Omnitech.NeuralDataFeed.CrossCutting;
using Omnitech.NeuralDataFeed.Data.Interfaces;
using Omnitech.NeuralDataFeed.Data.Repositories;
using Omnitech.NeuralDataFeed.Provider.Interfaces;
using Omnitech.NeuralDataFeed.Provider.Providers;
using Omnitech.NeuralDataFeed.Service.Interfaces;
using Omnitech.NeuralDataFeed.Service.Services;
using Omnitech.NeuralDataFeed.Service.Services.WebSockets;
using System.Net.WebSockets;

var builder = WebApplication.CreateBuilder(args);
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();
// Habilitar o middleware de WebSockets
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(120),
    AllowedOrigins = { "http://localhost:5000" }
};

NativeInjectorBootStrapper.RegisterHostDependencies(builder.Services, configuration);

var app = builder.Build();

app.UseWebSockets(webSocketOptions);

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocketHandler = app.Services.GetRequiredService<WebSocketHandler>();
        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await webSocketHandler.HandleAsync(context, webSocket);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

app.Run();

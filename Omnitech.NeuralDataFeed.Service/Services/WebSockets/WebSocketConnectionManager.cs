using System.Net.WebSockets;
using System.Text;

namespace Omnitech.NeuralDataFeed.Service.Services.WebSockets
{
    public class WebSocketConnectionManager
    {
        private readonly Dictionary<string, WebSocket> _sockets = new Dictionary<string, WebSocket>();

        public string AddSocket(WebSocket socket)
        {
            string socketId = Guid.NewGuid().ToString();
            _sockets.Add(socketId, socket);
            return socketId;
        }

        public async Task BroadcastMessageAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);

            foreach (var socket in _sockets.Values)
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(
                        new ArraySegment<byte>(buffer, 0, buffer.Length),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
            }
        }
    }
}

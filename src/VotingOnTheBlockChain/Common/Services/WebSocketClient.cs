using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Services
{
    public sealed class WebSocketClient
    {
        private ClientWebSocket _webSocket;
        private Uri _uri;
       
        private readonly CancellationTokenSource _cts;
        private bool _isReconnecting;
        public event EventHandler<string> OnMessageReceived;
        public event EventHandler<WebSocketState> OnConnectionStateChanged;

        public WebSocketClient(Uri uri)
        {
            _uri = uri;
            _webSocket = new ClientWebSocket();
            _cts = new CancellationTokenSource();
        }

        public async Task ConnectAsync()
        {
            try
            {
                await _webSocket.ConnectAsync(_uri, _cts.Token);
                OnConnectionStateChanged?.Invoke(this, _webSocket.State);
                ReceiveDataAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to WebSocket: {ex.Message}");
                await ReconnectAsync();
            }
        }

        public async Task SendAsync(string message)
        {
            
            var data = System.Text.Encoding.UTF8.GetBytes(message);
            var buffer = new ArraySegment<byte>(data);

            try
            {
                await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending data to WebSocket: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            _cts.Cancel();
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            OnConnectionStateChanged?.Invoke(this, _webSocket.State);
        }

        private async Task ReceiveDataAsync()
        {
            while (_webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var buffer = new ArraySegment<byte>(new byte[4096]);
                    var result = await _webSocket.ReceiveAsync(buffer, _cts.Token);
                    OnConnectionStateChanged?.Invoke(this, _webSocket.State);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", _cts.Token);
                    }
                    else
                    {
                        var json = System.Text.Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        //T data = JsonSerializer.Deserialize<T>(json);
                        ThreadPool.QueueUserWorkItem(state => OnMessageReceived?.Invoke(this, json));
                        
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving data from WebSocket: {ex.Message}");
                    await ReconnectAsync();
                }
            }
        }

        private async Task ReconnectAsync()
        {
            if (_isReconnecting)
            {
                return;
            }
            _isReconnecting = true;
            while (_webSocket.State != WebSocketState.Open)
            {
                try
                {
                    Console.WriteLine("Reconnecting to WebSocket...");
                    await Task.Delay(5000);
                    await ConnectAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reconnecting to WebSocket: {ex.Message}");
                }
            }
            _isReconnecting = false;
        }
    }


}


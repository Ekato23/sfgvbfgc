using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebApplication1
{
    using System;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Practika2;

    class Server
    {
        private static ConcurrentDictionary<string, ClientSession> _sessions = new();

        static async Task Main(string[] args)
        {
            int port = 5000;
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"[SERVER] Запущен на порту {port}...");

            while (true)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(tcpClient));
            }
        }

        private static async Task HandleClientAsync(TcpClient tcpClient)
        {
            var remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
            var sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);

            var session = new ClientSession
            {
                UniqueCode = sessionId,
                ConnectTime = DateTime.Now,
                ClientIp = remoteEndPoint.Address,
                ClientPort = remoteEndPoint.Port,
                Socket = tcpClient.Client,
                LastConfirmationTime = DateTime.Now,
                IsLicensed = true
            };

            _sessions.TryAdd(sessionId, session);
            Console.WriteLine($"[CONNECT] Клиент подключен: {session.ClientIp}:{session.ClientPort} [ID: {sessionId}]");

            try
            {
                using var stream = tcpClient.GetStream();
                byte[] buffer = new byte[4096];

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string jsonString = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var message = JsonSerializer.Deserialize<ViewModelMessage>(jsonString);

                    if (message != null)
                    {
                        Console.WriteLine($"[{sessionId}] Команда: {message.Command} | Данные: {message.Data}");

                        // Пример ответа клиенту
                        var responseObj = new ViewModelSend($"Сервер получил команду: {message.Command}", 200);
                        string responseJson = JsonSerializer.Serialize(responseObj);
                        byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {sessionId}: {ex.Message}");
            }
            finally
            {
                _sessions.TryRemove(sessionId, out _);
                tcpClient.Close();
                Console.WriteLine($"[DISCONNECT] Клиент отключился: {sessionId}");
            }
        }
    }
}

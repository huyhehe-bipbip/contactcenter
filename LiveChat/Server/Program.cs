using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Text;
using Shared;

namespace Server
{
    class Program
    {
        private static readonly ConcurrentDictionary<string, NetworkStream> _agents = new();
        private static readonly ConcurrentDictionary<string, NetworkStream> _clients = new();

        public static async Task Main()
        {
            var listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();
            while (true)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(tcpClient));
            }
        }

        private static async Task HandleClientAsync(TcpClient tcpClient)
        {
            string? registeredId = null;
            bool isAgent = false;
            var stream = tcpClient.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            try
            {
                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;
                    var packet = Packet.Deserialize(line);
                    if (packet == null) continue;
                    switch (packet.Type)
                    {
                        case MessageType.AgentReady:
                            registeredId = packet.SenderId;
                            isAgent = true;
                            _agents[registeredId] = stream;
                            Console.WriteLine($"[AGENT ONLINE] Agent {packet.SenderId} ready.");
                            break;
                        case MessageType.ClientInit:
                            registeredId = packet.SenderId;
                            isAgent = false;
                            _clients[registeredId] = stream;
                            Console.WriteLine($"[CLIENT YEU CAU] Client {packet.SenderId} requested chat.");
                            foreach (var agent in _agents)
                            {
                                var sessionId = "S_" + Guid.NewGuid().ToString("N")[..8];
                                var sessionPacket = new Packet
                                {
                                    Type = MessageType.SessionAssigned,
                                    SessionId = sessionId,
                                    SenderId = agent.Key,
                                    ReceiverId = registeredId
                                };
                                await SendPacket(agent.Value, sessionPacket);
                                await SendPacket(stream, sessionPacket);
                                Console.WriteLine($"[ROUTING] Client {registeredId} <-> Agent {agent.Key} (Session: {sessionId})");
                                break;
                            }
                            break;
                        case MessageType.ChatMessage:
                            Console.WriteLine($"[CHAT] {packet.SenderId} -> {packet.ReceiverId}: {packet.Content}");
                            if (_agents.TryGetValue(packet.ReceiverId, out var agentStream))
                                await SendPacket(agentStream, packet);
                            else if (_clients.TryGetValue(packet.ReceiverId, out var clientStream))
                                await SendPacket(clientStream, packet);
                            break;
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (!string.IsNullOrEmpty(registeredId))
                {
                    if (isAgent)
                        _agents.TryRemove(registeredId, out _);
                    else
                        _clients.TryRemove(registeredId, out _);
                }
                stream.Close();
                tcpClient.Close();
            }
        }

        private static async Task SendPacket(NetworkStream stream, Packet packet)
        {
            var data = Encoding.UTF8.GetBytes(packet.Serialize() + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
        }
    }
}
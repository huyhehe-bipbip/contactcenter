using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Shared;

namespace Server
{
    class Program
    {
        private static readonly ConcurrentDictionary<string, NetworkStream> _agents = new();
        private static readonly ConcurrentDictionary<string, NetworkStream> _clients = new();
        private static int _lastAgentIndex = -1;

        public static async Task Main()
        {
            var listener = new TcpListener(IPAddress.Any, 5050);
            listener.Start();
            Console.WriteLine("[SERVER STARTED] Listening on port 5050...");

            while (true)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleClientAsync(tcpClient));
            }
        }

        private static async Task HandleClientAsync(TcpClient tcpClient)
        {
            var stream = tcpClient.GetStream();
            string? registeredId = null;
            bool isAgent = false;

            try
            {
                while (true)
                {
                    var jsonStr = await Packet.ReceiveJsonAsync(stream);
                    if (string.IsNullOrEmpty(jsonStr)) break;

                    var packet = JsonSerializer.Deserialize<Packet>(jsonStr);
                    if (packet == null) continue;

                    switch (packet.Type)
                    {
                        case "AGENT_LOGIN":
                            registeredId = packet.Agent_ID;
                            isAgent = true;
                            if (registeredId != null)
                            {
                                _agents[registeredId] = stream;
                                Console.WriteLine($"[AGENT ONLINE] Agent {registeredId} ready.");
                            }
                            break;

                        case "CUSTOMER_REQ":
                            registeredId = packet.Customer_ID;
                            isAgent = false;
                            if (registeredId != null)
                            {
                                _clients[registeredId] = stream;
                                Console.WriteLine($"[CLIENT YEU CAU] Khách {registeredId} ({packet.Customer_Name}) requested chat.");
                                await AssignAgentToClient(registeredId, packet.Customer_Name, stream);
                            }
                            break;

                        case "CHAT_MSG":
                            Console.WriteLine($"[CHAT] {packet.Sender}: {packet.Content}");
                            await ForwardMessage(packet, jsonStr);
                            break;

                        case "END_SESSION":
                            Console.WriteLine($"[KET THUC] Session {packet.Session_ID} closed by {packet.Sender}.");
                            await ForwardMessage(packet, jsonStr);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOI] {registeredId}: {ex.Message}");
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
                Console.WriteLine($"[NGAT KET NOI] {registeredId ?? "Unknown"} disconnected.");
            }
        }

        private static async Task AssignAgentToClient(string customerId, string? customerName, NetworkStream clientStream)
        {
            if (_agents.IsEmpty)
            {
                Console.WriteLine($"[WARNING] Khách {customerId} đang chờ nhưng không có Agent nào online!");
                return;
            }

            var agentList = _agents.ToArray();
            _lastAgentIndex = (_lastAgentIndex + 1) % agentList.Length;

            var selectedAgent = agentList[_lastAgentIndex];
            var sessionId = "S_" + Guid.NewGuid().ToString("N")[..8];

            var sessionPacket = new Packet
            {
                Type = "SESSION_ASSIGNED",
                Session_ID = sessionId,
                Agent_ID = selectedAgent.Key,
                Customer_ID = customerId,
                Customer_Name = customerName
            };

            string jsonStr = JsonSerializer.Serialize(sessionPacket);

            await Packet.SendJsonAsync(selectedAgent.Value, jsonStr);
            await Packet.SendJsonAsync(clientStream, jsonStr);

            Console.WriteLine($"[ROUTING] {customerId} <-> {selectedAgent.Key} (Session: {sessionId})");
        }

        private static async Task ForwardMessage(Packet packet, string rawJson)
        {
            if (!string.IsNullOrEmpty(packet.Customer_ID) && _clients.TryGetValue(packet.Customer_ID, out var clientStream))
            {
                if (packet.Sender != packet.Customer_ID)
                {
                    await Packet.SendJsonAsync(clientStream, rawJson);
                }
            }

            if (!string.IsNullOrEmpty(packet.Agent_ID) && _agents.TryGetValue(packet.Agent_ID, out var agentStream))
            {
                if (packet.Sender != packet.Agent_ID)
                {
                    await Packet.SendJsonAsync(agentStream, rawJson);
                }
            }
        }
    }
}
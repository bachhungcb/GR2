using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client.TelemetryCore.TelemetryClient;
using SIEMServer.Interfaces; // Cần thiết cho IServiceProvider
using SIEMServer.Service;
using SIEMServer.Service.Channel; // Cần thiết cho IPacketHandlerService

namespace SIEMServer.TCP
{
    public sealed class TCPServer
    {
        // 1. DEPENDENCIES
        private readonly PacketChannelService _channel;
        private readonly BlacklistService _blacklistService;

        public TCPServer(
            PacketChannelService channel,
            BlacklistService blacklistService)
        {
            _channel = channel;
            _blacklistService = blacklistService;
        }

        public async Task RunAsync()
        {
            var ipEndPoint = new IPEndPoint(IPAddress.Any, 8888);
            TcpListener listener = new(ipEndPoint);

            try
            {
                listener.Start();
                Console.WriteLine("Server is listening at port 8888");
                while (true) // Vòng lặp Client
                {
                    try
                    {
                        Console.WriteLine("Waiting for new agent connection....");
                        using TcpClient handler = await listener.AcceptTcpClientAsync();
                        var agentIp = handler.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                        Console.WriteLine($"Agent connected from: {agentIp}");

                        await using NetworkStream stream = handler.GetStream();

                        while (true) // Vòng lặp Tin nhắn (Message Loop)
                        {
                            Console.WriteLine($"{DateTime.Now} " +
                                              $"[SERVER] Begin receiving message...");
                            byte[] headerBuffer = new byte[4];
                            int bytesRead;
                            try
                            {
                                // ----- ĐỌC HEADER (4 BYTE) -----
                                int headerBytesRead = await stream.ReadAsync(headerBuffer, 0, 4);
                                if (headerBytesRead < 4)
                                {
                                    break; // Client đã ngắt kết nối
                                }

                                int packetLength = BitConverter.ToInt32(headerBuffer, 0);
                                packetLength = IPAddress.NetworkToHostOrder(packetLength);

                                // ----- ĐỌC PACKET (packetLength BYTE) -----
                                byte[] jsonBuffer = new byte[packetLength];
                                int totalBytesRead = 0;
                                int bytesLeft = packetLength;

                                while (totalBytesRead < packetLength)
                                {
                                    bytesRead = await stream.ReadAsync(jsonBuffer, totalBytesRead, bytesLeft);
                                    if (bytesRead == 0) break;
                                    totalBytesRead += bytesRead;
                                    bytesLeft -= bytesRead;
                                }

                                // ----- CHUYỂN GIAO (HAND OFF) GÓI TIN -----
                                if (totalBytesRead == packetLength)
                                {
                                    Console.WriteLine($"{DateTime.Now} " +
                                                      $"[SERVER] Sending raw packet...");
                                    var rawPacket = new RawPacket()
                                    {
                                        JsonBuffer = jsonBuffer,
                                        AgentIp = agentIp,
                                    };

                                    //Put packet into queue
                                    await _channel.WriteAsync(rawPacket);
                                    _ = Task.Run(async () =>
                                    {
                                        // (Chúng ta phải 'Deserialize' (Giải mã) 📖
                                        //  lại 1 lần nữa ở đây, nhưng nó rất nhanh ⚡️)
                                        try
                                        {
                                            var telemetryData =
                                                JsonSerializer.Deserialize<Telemetry.Telemetry>(rawPacket.JsonBuffer);
                                            if (telemetryData != null)
                                            {
                                                // Gọi (Call) logic "Phát hiện" (Detection) 🕵️‍♂️ / "Hành động" (Action) ⛔
                                                // (Bây-giờ nó chạy (runs) trên một luồng (thread) 
                                                //  riêng biệt 🏃‍♀️, không "chặn" (blocking) 🚫
                                                //  "Người phục vụ" (Waiter) ⚡️)
                                                await _blacklistService.FilterRules(telemetryData, stream, agentIp);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            // "Bắt" (Catch) bất kỳ lỗi (errors) "Bắn và Quên" (Fire-and-Forget) 🔥 nào
                                            Console.WriteLine(
                                                $"[HOT-PATH ERROR] Lỗi Phát hiện (Detection) 🕵️‍♂️: {ex.Message}");
                                        }
                                    });
                                }
                                else
                                {
                                    Console.WriteLine("[LỖI] Nhận được packet không đầy đủ.");
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"\nRead data Error: {ex.Message}");
                                break;
                            }
                        } // Kết thúc Vòng lặp Tin nhắn

                        Console.WriteLine($"\nAgent {agentIp} has disconnected.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"A connection encounter an error: {ex.Message}");
                    }
                } // Kết thúc Vòng lặp Client
            }
            catch (Exception ex)
            {
                Console.WriteLine($"There was an error: {ex.Message}");
            }
        }
    }
}
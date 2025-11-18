using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIEMServer.Context;
using SIEMServer.Interfaces;
using SIEMServer.Model;
using SIEMServer.Service.Channel;

namespace SIEMServer.Service;

/// <summary>
/// Dịch vụ Nền (Background Service) ⚙️ "Luồng Lạnh" (Cold Path) 🐢.
/// Nó chạy (runs) trên một luồng (thread) riêng biệt.
/// </summary>
public sealed class PacketProcessingService : BackgroundService
{
    private readonly ILogger<PacketProcessingService> _logger;
    private readonly PacketChannelService _channel;
    private readonly IServiceScopeFactory _scopeFactory;

    public PacketProcessingService(
        ILogger<PacketProcessingService> logger,
        PacketChannelService channel, // Queue
        IServiceScopeFactory scopeFactory) // factory
    {
        _logger = logger;
        _channel = channel;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Service (Kitchen) has been started");
        Console.WriteLine("[SERVICE] Has been started");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Chờ packet ĐẦU TIÊN
                await _channel.WaitForReadAsync(stoppingToken);

                // 2. Tạo một lô (batch) rỗng
                var batch = new List<RawPacket>();

                // 3. "Xả" (Drain) hàng đợi nhanh nhất có thể vào lô
                //    (Giới hạn lô 500 packet để tránh DB context quá lớn)
                while (batch.Count < 500 && _channel.TryRead(out RawPacket packet))
                {
                    batch.Add(packet);
                }

                // 4. Nếu lô có dữ liệu, xử lý nó MỘT LẦN DUY NHẤT
                if (batch.Any())
                {
                    try
                    {
                        // 5. Tạo MỘT scope duy nhất cho TOÀN BỘ lô
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var dbContext = scope.ServiceProvider
                                .GetRequiredService<SiemDbContext>();

                            _logger.LogInformation($"Processing a batch of {batch.Count} packets.");

                            // --- BƯỚC A: Deserialize và Lấy Agent IDs ---
                            var telemetryDataList = new List<Telemetry.Telemetry>();
                            foreach (var packet in batch)
                            {
                                // Giải mã (Deserialize) packet
                                var telemetryData = JsonSerializer
                                    .Deserialize<Telemetry.Telemetry>(packet.JsonBuffer);

                                if (telemetryData != null)
                                {
                                    telemetryData.AgentIp = packet.AgentIp; // Gán IP vào để dùng sau
                                    telemetryDataList.Add(telemetryData);
                                }
                            }

                            // Lấy TẤT CẢ Agent ID cần thiết trong lô
                            var agentIdsInBatch = telemetryDataList
                                .Select(t => t.AgentId).Distinct().ToList();

                            // --- BƯỚC B: Truy vấn Agents MỘT LẦN DUY NHẤT ---
                            // (Giải quyết vấn đề 330ms của FirstOrDefaultAsync)
                            var existingAgents = await dbContext.Agents
                                .Where(a => agentIdsInBatch.Contains(a.Id))
                                .ToDictionaryAsync(a => a.Id, stoppingToken);

                            var newAgents = new List<Agent>();
                            var newSnapshots = new List<TelemetrySnapshots>();

                            // --- BƯỚC C: Xử lý lô (batch) trong bộ nhớ ---
                            foreach (var telemetryData in telemetryDataList)
                            {
                                if (telemetryData.Alerts != null && telemetryData.Alerts.Any())
                                {
                                    _logger.LogWarning(
                                        $"[ALERTS BATCH] Agent {telemetryData.AgentId} reported {telemetryData.Alerts.Count} alerts:");
                                    foreach (var alert in telemetryData.Alerts)
                                    {
                                        _logger.LogWarning(
                                            $" -> Blocked '{alert.ProcessName}' (PID: {alert.Pid}) due to rule: {alert.MatchedRule}");
                                    }
                                    //LƯU VÀO DANH SÁCH ĐỂ INSERT DB
                                    var alertEntries = telemetryData.Alerts.Select(a => new AlertEntry
                                    {
                                        Id = Guid.NewGuid(),
                                        AgentId = telemetryData.AgentId,
                                        ProcessName = a.ProcessName,
                                        Pid = a.Pid,
                                        MatchedRule = a.MatchedRule,
                                        Timestamp = a.Timestamp
                                    }).ToList();
                                    await dbContext.Alerts.AddRangeAsync(alertEntries, stoppingToken);
                                }
                                
                                // Tìm agent từ Dictionary (CỰC NHANH)
                                if (!existingAgents.TryGetValue(telemetryData.AgentId, out Agent agent))
                                {
                                    // Nếu không có, tạo agent MỚI (chưa lưu)
                                    agent = new Agent
                                    {
                                        Id = telemetryData.AgentId,
                                        HostName = telemetryData.HostName ?? "Chưa rõ", // Sẽ được cập nhật sau
                                        FirstSeen = DateTime.Now
                                    };
                                    newAgents.Add(agent); // Thêm vào list agent mới
                                    existingAgents.Add(agent.Id, agent); // Thêm vào dictionary
                                }
                                else
                                {
                                    //Cập nhật tên nếu nó thay đổi hoặc đang là "Chưa rõ"
                                    if (!string.IsNullOrEmpty(telemetryData.HostName) && agent.HostName != telemetryData.HostName)
                                    {
                                        agent.HostName = telemetryData.HostName;
                                    }
                                }

                                agent.LastSeen = DateTime.Now;

                                // Tạo (Create) Gói tin "mẹ" (Snapshot) 📦
                                var snapshot = new TelemetrySnapshots
                                {
                                    Id = Guid.NewGuid(),
                                    Agent = agent, // Gán object đã theo dõi (tracked)
                                    Timestamp = DateTime.Now,
                                    AgentIpAddress = telemetryData.AgentIp
                                };

                                // Ánh xạ (Map) Tiến trình (Processes) 🖥️
                                snapshot.ProcessEntries = telemetryData.Processes
                                    .Select(p_json => new ProcessEntries
                                    {
                                        Pid = p_json.Pid,
                                        Name = p_json.Name,
                                        FilePath = p_json.FilePath ?? string.Empty,
                                        Commandline = p_json.CommandLine ?? string.Empty
                                    }).ToList();

                                // Ánh xạ (Map) Kết nối (Connections) 📡
                                snapshot.ConnectionEntries = telemetryData.Connections
                                    .Select(c_json => new ConnectionEntries
                                    {
                                        LocalEndPointAddr = c_json.LocalEndPointAddr,
                                        RemoteEndPointAddr = c_json.RemoteEndPointAddr,
                                        State = c_json.State
                                    }).ToList();

                                newSnapshots.Add(snapshot);
                                
                            }

                            // --- BƯỚC D: Lưu (Save) vào DB MỘT LẦN DUY NHẤT ---
                            if (newAgents.Any())
                            {
                                // Thêm tất cả agent mới
                                await dbContext.Agents.AddRangeAsync(newAgents, stoppingToken);
                            }

                            // Thêm tất cả snapshot mới
                            await dbContext.Snapshot.AddRangeAsync(newSnapshots, stoppingToken);

                            // Lưu tất cả thay đổi trong 1 giao dịch (transaction)
                            await dbContext.SaveChangesAsync(stoppingToken);

                            _logger.LogInformation($"[BATCH SAVED] Saved {newSnapshots.Count} snapshots.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error (kitchen) while processing BATCH");
                    }
                } //Kết thúc if (batch.Any())
            } //Go back to loop back 'WaitForReadAsync'
        }
        catch (OperationCanceledException e)
        {
            _logger.LogInformation("Service (kitchen) cancelled");
        }
    }
}
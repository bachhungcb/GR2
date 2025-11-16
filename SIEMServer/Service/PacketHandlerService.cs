using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIEMServer.Command;
using SIEMServer.Context;
using SIEMServer.Interfaces;
using SIEMServer.Model;
using SIEMServer.Service;
using SIEMServer.Telemetry;

public sealed class PacketHandlerService : IPacketHandlerService
{
    private readonly SiemDbContext _dbContext;
    private readonly ILogger<PacketHandlerService> _logger;
    private readonly GetHostNameService _hostNameService;
    private readonly BlacklistService _blacklistService;


    // 1. "Tiêm" (Inject) các dịch vụ (services) Scoped (an toàn)
    public PacketHandlerService(
        SiemDbContext dbContext,
        GetHostNameService hostNameService,
        BlacklistService blacklistService,
        ILogger<PacketHandlerService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _hostNameService = hostNameService;
        _blacklistService = blacklistService;
    }

    // 2. Đây là nơi TẤT CẢ logic nghiệp vụ (business logic) cũ được chuyển đến
    public async Task ProcessPacketAsync(byte[] jsonBuffer, string remoteIpAddress, NetworkStream replyStream)
    {
        try
        {
            // ----- A. GIẢI MÃ (DESERIALIZE) -----
            var jsonDataSegment = new ArraySegment<byte>(jsonBuffer, 0, jsonBuffer.Length);
            var telemetryData = JsonSerializer.Deserialize<Telemetry>(jsonDataSegment);

            if (telemetryData.Alerts != null && telemetryData.Alerts.Any())
            {
                _logger.LogWarning(
                    $"[ALERTS RECEIVED] Agent {telemetryData.AgentId} reported {telemetryData.Alerts.Count} alerts:");
                foreach (var alert in telemetryData.Alerts)
                {
                    _logger.LogWarning(
                        $" -> Blocked '{alert.ProcessName}' (PID: {alert.Pid}) due to rule: {alert.MatchedRule}");

                    // TODO: Ở đây, bạn có thể lưu 'alert' vào một bảng 
                    // CSDL mới (ví dụ: 'dbo.AlertHistory')
                }
            }

            if (telemetryData == null)
            {
                Console.WriteLine("[ERROR] Invalid JSON Data");
                return;
            }

            // ----- C. IN (PRINT) RA CONSOLE -----
            //LogToConsole(telemetryData, localNames, remoteNames);

            // ----- D. LƯU (SAVE) VÀO CƠ SỞ DỮ LIỆU (DATABASE) 💾 -----
            // Tìm (Find) hoặc Tạo (Create) Agent 🤖
            Agent agent = await _dbContext.Agents
                .FirstOrDefaultAsync(a => a.Id == telemetryData.AgentId);

            if (agent == null)
            {
                agent = new Agent
                {
                    Id = telemetryData.AgentId,
                    HostName = "Chưa rõ", // TODO
                    FirstSeen = DateTime.UtcNow
                };
                _dbContext.Agents.Add(agent);
            }

            agent.LastSeen = DateTime.UtcNow;

            // Tạo (Create) Gói tin "mẹ" (Snapshot) 📦
            var snapshot = new TelemetrySnapshots
            {
                Id = Guid.NewGuid(),
                Agent = agent,
                Timestamp = DateTime.UtcNow,
                AgentIpAddress = remoteIpAddress
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


            // Ánh xạ (Map) Kết nối (Connections) 📡 (TÁI SỬ DỤNG kết quả tra cứu DNS 🌐)
            snapshot.ConnectionEntries = telemetryData.Connections
                .Select(c_json => new ConnectionEntries
                {
                    LocalEndPointAddr = c_json.LocalEndPointAddr,
                    RemoteEndPointAddr = c_json.RemoteEndPointAddr,
                    State = c_json.State
                    // Chúng ta CỐ TÌNH (INTENTIONALLY) để 'LocalEndPointName'
                    // và 'RemoteEndPointName' là NULL.
                    // Dịch vụ (Service) 🐢 "Luồng Lạnh" (Cold Path) sẽ điền (fill) chúng sau.
                }).ToList();

            // Lưu (Save)
            _dbContext.Snapshot.Add(snapshot);
            await _dbContext.SaveChangesAsync();

            Console.WriteLine($"{DateTime.Now} " +
                              $"[SAVED] Saved Snapshot with {snapshot.ProcessEntries.Count} processes." +
                              $" (Agent :{remoteIpAddress})");
        }
        catch (Exception ex)
        {
            // Bắt (Catch) lỗi Giải mã (Deserialize) hoặc lỗi DB (Database)
            Console.WriteLine($"[PACKET HANDLER ERROR] Lỗi khi xử lý gói tin: {ex.Message}");
        }
    }

    // Tách logic In (Print) ra hàm riêng
    private void LogToConsole(Telemetry telemetryData, string[] localNames, string[] remoteNames)
    {
        Console.WriteLine($"\n--- [RECEIVED PACKET AT {DateTime.Now:HH:mm:ss}] ---");
        Console.WriteLine($"Processes ({telemetryData.Processes.Count}):");
        foreach (var p in telemetryData.Processes)
        {
            Console.WriteLine($"  [P] {p}");
        }

        Console.WriteLine($"Connections ({telemetryData.Connections.Count}):");
        for (int i = 0; i < telemetryData.Connections.Count; i++)
        {
            var c = telemetryData.Connections[i];
            Console.WriteLine(
                $"  [C] {c.LocalEndPointAddr} ({localNames[i]}) -> {c.RemoteEndPointAddr} ({remoteNames[i]}) [{c.State}]");
        }

        Console.WriteLine("--- [END OF PACKET] ---");
    }
}
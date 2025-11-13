using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.TelemetryCore.TelemetryClient;
using SIEMServer.Command;
using SIEMServer.Context;
using SIEMServer.Model;

namespace SIEMServer.Service;

public sealed class BlacklistService
{
    // 1. Cache memory
    private readonly List<BlacklistedProcess> _cachedRules;
    private readonly ILogger<BlacklistService> _logger;

    // 2. Hàm khởi tạo (Constructor) (Chạy MỘT LẦN khi Server 🖥️ khởi động)
    //    Chúng ta "tiêm" (inject) IServiceProvider 🏭 (Nhà máy)
    //    để một Singleton 📓 có thể lấy (get) một DbContext 🌉 (Scoped) 
    public BlacklistService(IServiceProvider serviceProvider, ILogger<BlacklistService> logger)
    {
        _logger = logger;
        _logger.LogInformation("Loading blacklist into cached");

        try
        {
            // 3. TẠO một "phạm vi" (scope) 📦 tạm thời CHỈ để đọc (read) DB
            using (var scope = serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SiemDbContext>();

                // 4. LOGIC "TẢI MỘT LẦN" (LOAD-ONCE) 💾
                _cachedRules = dbContext.BlacklistedProcesses
                    .AsNoTracking()
                    .ToList();
            }

            _logger.LogInformation($"ĐÃ TẢI THÀNH CÔNG (SUCCESSFULLY LOADED) " +
                                   $"{_cachedRules.Count} quy tắc (rules) vào bộ nhớ đệm (cache) 🧠.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LỖI NGHIÊM TRỌNG (FATAL ERROR): " +
                                 "Không thể tải (load) 'Cuốn sổ đen' (Blacklist) 📓!");
            _cachedRules = new List<BlacklistedProcess>(); // Khởi tạo (Init) một danh sách (list) rỗng
        }
    }

    // 5. HÀM "SIÊU TỐC" (INSTANT) ⚡️
    //    Hàm này chỉ đọc (read) từ bộ nhớ 🧠, không đọc (read) từ DB 💾
    public List<BlacklistedProcess> GetRules()
    {
        return _cachedRules;
    }

    public async Task FilterRules(
        Telemetry.Telemetry telemetryData,
        NetworkStream replyStream,
        string agentIp)
    {
        try
        {
            //Black list
            var blackListRules = GetRules();

            if (blackListRules.Any())
            {
                //Loop through every single process that Agent sent
                foreach (var incomingProcess in telemetryData.Processes)
                {
                    //Loop through every rules in blacklist rule
                    foreach (var rule in blackListRules)
                    {
                        // --- CHẠY LOGIC SO KHỚP (MATCHING LOGIC) ---
                        // Quy tắc 1: Khớp (Match) Tên (Name)
                        bool isMatched = false; //Flags for indicating rule violence)
                        string matchedBy = "";
                        if (!string.IsNullOrEmpty(rule.Name) &&
                            rule.Name.Equals(incomingProcess.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            isMatched = true;
                            matchedBy = $"Name: {rule.Name}";
                        }

                        // Quy tắc 2: Khớp (Match) Đường dẫn Tệp (File Path)
                        if (!isMatched && !string.IsNullOrEmpty(rule.FilePath) &&
                            //ensures that file path is not NULL
                            !string.IsNullOrEmpty(incomingProcess.FilePath) &&
                            //Path contains rule filepath
                            incomingProcess.FilePath.Contains(rule.FilePath, StringComparison.OrdinalIgnoreCase)
                           )
                        {
                            isMatched = true;
                            matchedBy = $"Path: {rule.FilePath}";
                        }

                        // Quy tắc 3: Khớp (Match) Dòng lệnh (Commandline)
                        if (!isMatched && !string.IsNullOrEmpty(rule.Commandline) &&
                            incomingProcess.CommandLine.Contains(rule.Commandline,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            isMatched = true;
                            matchedBy = $"Command Line: {rule.Commandline}";
                        }

                        // Quy tắc 4: Khớp (Match) Hash
                        // if (!isMatched && !string.IsNullOrEmpty(rule.HashValue) &&
                        //     rule.HashValue.Equals(incomingProcess.HashValue,StringComparison.OrdinalIgnoreCase))
                        // {
                        //     isMatched = true;
                        //     matchedBy = $"Hash Value: {rule.HashValue}";
                        // } 

                        //TODO: Implement rule 4, hash matching

                        // --- XỬ LÝ VI PHẠM (HANDLE MATCH) ---
                        if (isMatched)
                        {
                            //Found violence
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"{DateTime.Now} " +
                                              $"[!!! BLACKLIST ALERT !!! ] " +
                                              $" Agent '{telemetryData.AgentId}'" +
                                              $" is running a forbidden process: {incomingProcess.Name} Violence ({matchedBy}) rule");
                            Console.ResetColor();

                            //TODO: Send block message
                            await SendBlockCommandAsync(replyStream, incomingProcess.Pid, agentIp);
                            // Thoát (Break) khỏi vòng lặp 'rule' (vì chúng ta đã tìm thấy 1 vi phạm)
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PACKET HANDLER ERROR] Error when handling black list: {ex.Message}");
        }
    }

    // (TODO sau này: Thêm (Add) một phương thức (method) "RefreshCache"
    //  để chúng ta có thể cập nhật (update) các quy tắc (rules) mà không cần khởi động (restart) lại)

    private async Task SendBlockCommandAsync(
        NetworkStream stream,
        int pidToBlock,
        string agentIp)
    {
        try
        {
            Console.WriteLine($"[ACTION] Sending command BLOCK" +
                              $" for PID: {pidToBlock} (Agent : {agentIp})");
            // 1. Create command packet
            var command = new ServerCommand
            {
                CommandType = "BLOCK_PROCESS_PID",
                Target = pidToBlock.ToString()
            };

            // 2. Serialize 
            byte[] commandPacket = JsonSerializer.SerializeToUtf8Bytes(command);

            // 3. Framing command 
            int packetLength = commandPacket.Length;
            int networkOrderLength = IPAddress.HostToNetworkOrder(packetLength);
            byte[] header = BitConverter.GetBytes(networkOrderLength);

            // 4. Send command to client
            // Send 4 byte header
            await stream.WriteAsync(header, 0, header.Length);
            //Send packet
            await stream.WriteAsync(commandPacket, 0, commandPacket.Length);

            Console.WriteLine("[ACTION] SENT command successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ACTION ERROR] Failed to send command: {ex.Message}");
        }
    }
}
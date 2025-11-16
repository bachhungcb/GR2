using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace MyWorkerService.Services;

// [MỚI] Định nghĩa DTO (Data Transfer Object) để
// Agent "hiểu" được dữ liệu JSON từ Server
public class BlacklistedProcess
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string FilePath { get; set; }
    public string? Commandline { get; set; }
    public string? HashValue { get; set; }
}

// [MỚI] Định nghĩa gói tin Cảnh báo (Alert)
public class Alert
{
    public string ProcessName { get; set; }
    public int Pid { get; set; }
    public string MatchedRule { get; set; }
    public DateTime Timestamp { get; set; }
}

public class LocalBlacklistService
{
    private readonly ILogger<LocalBlacklistService> _logger;

    // Sử dụng ConcurrentBag để an toàn luồng (thread-safe)
    private ConcurrentBag<BlacklistedProcess> _rules = new();

    public LocalBlacklistService(ILogger<LocalBlacklistService> logger)
    {
        _logger = logger;
    }

    // Hàm để RuleSyncService cập nhật cache
    public void UpdateRules(List<BlacklistedProcess> newRules)
    {
        _rules = new ConcurrentBag<BlacklistedProcess>(newRules);
        _logger.LogInformation($"Updated local blacklist cache with {newRules.Count} rules.");
    }

    // Logic lọc và chặn chính
    public List<Alert> FilterAndBlock(
        List<GetProcessService.ProcessJsonElement> processes,
        HandleCommandService commandHandler)
    {
        var alerts = new List<Alert>();
        if (processes == null || !_rules.Any())
        {
            return alerts;
        }

        foreach (var incomingProcess in processes)
        {
            foreach (var rule in _rules)
            {
                // --- CHẠY LOGIC SO KHỚP (MATCHING LOGIC) ---
                bool isMatched = false;
                string matchedBy = "";

                // Quy tắc 1: Tên (Name)
                if (!string.IsNullOrEmpty(rule.Name) &&
                    rule.Name.Equals(incomingProcess.Name, StringComparison.OrdinalIgnoreCase))
                {
                    isMatched = true;
                    matchedBy = $"Name: {rule.Name}";
                }

                // Quy tắc 2: Đường dẫn (File Path)
                if (!isMatched && !string.IsNullOrEmpty(rule.FilePath) &&
                    !string.IsNullOrEmpty(incomingProcess.FilePath) &&
                    incomingProcess.FilePath.Contains(rule.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    isMatched = true;
                    matchedBy = $"Path: {rule.FilePath}";
                }

                // Quy tắc 3: Dòng lệnh (Commandline)
                if (!isMatched && !string.IsNullOrEmpty(rule.Commandline) &&
                    incomingProcess.CommandLine.Contains(rule.Commandline,
                        StringComparison.OrdinalIgnoreCase))
                {
                    isMatched = true;
                    matchedBy = $"Command Line: {rule.Commandline}";
                }

                // --- XỬ LÝ VI PHẠM (HANDLE MATCH) ---
                if (isMatched)
                {
                    _logger.LogWarning(
                        $"[LOCAL BLOCK] Found violation: {incomingProcess.Name} ({incomingProcess.Pid})");

                    // 1. TẠO CẢNH BÁO (ALERT)
                    alerts.Add(new Alert
                    {
                        ProcessName = incomingProcess.Name,
                        Pid = incomingProcess.Pid,
                        MatchedRule = matchedBy,
                        Timestamp = DateTime.UtcNow
                    });

                    // 2. CHẶN NGAY LẬP TỨC
                    var command = new ServerCommand
                    {
                        CommandType = "BLOCK_PROCESS_PID",
                        Target = incomingProcess.Pid.ToString()
                    };
                    commandHandler.ExecuteCommand(command);

                    // Thoát (Break) khỏi vòng lặp 'rule' (vì đã tìm thấy vi phạm)
                    break;
                }
            }
        }

        return alerts;
    }
}
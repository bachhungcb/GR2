using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Security.Cryptography;

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
            // [SỬA] Khởi tạo hash là null
            string? calculatedHash = null;

            foreach (var rule in _rules)
            {
                // --- CHẠY LOGIC SO KHỚP (MATCHING LOGIC) ---
                bool isMatched = false;
                string matchedBy = "";

                // Quy tắc 1: Tên (Name) (Giữ nguyên)
                if (!string.IsNullOrEmpty(rule.Name) &&
                    rule.Name.Equals(incomingProcess.Name, StringComparison.OrdinalIgnoreCase))
                {
                    isMatched = true;
                    matchedBy = $"Name: {rule.Name}";
                }

                // Quy tắc 2: Đường dẫn (File Path) (Giữ nguyên)
                if (!isMatched && !string.IsNullOrEmpty(rule.FilePath) &&
                    !string.IsNullOrEmpty(incomingProcess.FilePath) &&
                    incomingProcess.FilePath.Contains(rule.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    isMatched = true;
                    matchedBy = $"Path: {rule.FilePath}";
                }

                // Quy tắc 3: Dòng lệnh (Commandline) (Giữ nguyên)
                if (!isMatched && !string.IsNullOrEmpty(rule.Commandline) &&
                    incomingProcess.CommandLine.Contains(rule.Commandline,
                        StringComparison.OrdinalIgnoreCase))
                {
                    isMatched = true;
                    matchedBy = $"Command Line: {rule.Commandline}";
                }

                // [MỚI] Quy tắc 4: Giá trị Hash (Hash Value)
                if (!isMatched && !string.IsNullOrEmpty(rule.HashValue))
                {
                    // Chỉ tính hash nếu chúng ta chưa tính
                    if (calculatedHash == null)
                    {
                        calculatedHash = CalculateSHA256(incomingProcess.FilePath);
                    }

                    // So sánh hash (nếu file tồn tại và có thể đọc được)
                    if (calculatedHash != null &&
                        calculatedHash.Equals(rule.HashValue, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatched = true;
                        matchedBy = $"HashValue: {rule.HashValue.Substring(0, 10)}..."; // Rút gọn hash để log
                    }
                }

                // --- XỬ LÝ VI PHẠM (HANDLE MATCH) ---
                if (isMatched)
                {
                    _logger.LogWarning($"[LOCAL BLOCK] Found violation: {incomingProcess.Name} ({incomingProcess.Pid})");

                    // 1. Tạo Alert
                    alerts.Add(new Alert
                    {
                        ProcessName = incomingProcess.Name,
                        Pid = incomingProcess.Pid,
                        MatchedRule = matchedBy,
                        Timestamp = DateTime.Now
                    });

                    // 2. [QUAN TRỌNG] GỌI LỆNH CHẶN
                    var command = new ServerCommand
                    {
                        CommandType = "BLOCK_PROCESS_PID",
                        Target = incomingProcess.Pid.ToString()
                    };
                    
                    // Gọi hàm ExecuteCommand để thực hiện Kill/Taskkill
                    commandHandler.ExecuteCommand(command);

                    break; // Thoát vòng lặp rules
                }
            }
        }

        return alerts;
    }

    private string? CalculateSHA256(string filePath)
    {
        // Kiểm tra xem file có tồn tại không
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using (var sha256 = SHA256.Create())
            {
                // Mở file với FileShare.Read để tránh lỗi file đang bị khóa
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var hash = sha256.ComputeHash(fileStream);
                    // Chuyển mảng byte hash thành một chuỗi Hex (viết hoa)
                    return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
                }
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, $"File bị khóa (locked) khi đang tính hash: {filePath}");
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, $"Không có quyền (permission) đọc file để tính hash: {filePath}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Lỗi không xác định khi tính hash file: {filePath}");
            return null;
        }
    }
}
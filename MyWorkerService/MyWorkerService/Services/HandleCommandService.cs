using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics; //For Process.GetProcessById().Kill()

namespace MyWorkerService.Services;

public class ServerCommand
{
    public string CommandType { get; set; }
    public string Target { get; set; }
}

public class HandleCommandService
{
    private readonly ILogger<HandleCommandService> _logger;

    // Cached for preventing spamming blocking IP
    private readonly HashSet<string> _blockedIpCache = new HashSet<string>();


    public HandleCommandService(ILogger<HandleCommandService> logger)
    {
        _logger = logger;
    }

    // --- 1. EXECUTION MAIN ---
    // This is the main execution part
    public void ExecuteCommand(ServerCommand command)
    {
        _logger.LogWarning($"[EXECUTE] Processing Command: {command.CommandType} from {command.Target}");
        try
        {
            // 1. Route the command
            switch (command.CommandType)
            {
                case "BLOCK_PROCESS_PID":
                    HandleBlockProcess(command.Target);
                    break;

                case "BLOCK_IP":
                    HandleBlockIp(command.Target);
                    break;

                default:
                    _logger.LogWarning($"Unknown command: {command.CommandType}");
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Error when executing command: {command.CommandType}");
        }
    }

    // --- 2. BLOCK LOGIC ---
    //  2.1. Block process
    private void HandleBlockProcess(string pidString)
    {
        _logger.LogInformation($"Received BLOCK command for PID: {pidString}");

        if (int.TryParse(pidString, out int pid))
        {
            try
            {
                // Copy lại đoạn code TaskKill /F /PID mà bạn đang dùng tốt
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/F /PID {pid} /T",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi);
                    _logger.LogWarning($"[TASKKILL SENT] Executed taskkill /F /PID {pid}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[FATAL ERROR] Could not kill PID {pid}");
                }
            }
            catch (ArgumentException)
            {
                _logger.LogWarning($"Can NOT find PID: {pid}. Process might have been closed unexpectedly.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error when attemping to KILL Process: {pid}");
            }
        }
    }

    // 2.2. Block IP
    private void HandleBlockIp(string ipAddress)
    {
        _logger.LogInformation("[INF] Begin Blocking IP ");
        // 1. KIỂM TRA CACHE TRƯỚC
        // Nếu IP này đã được xử lý trong phiên chạy này rồi thì bỏ qua
        // Giúp giảm tải CPU và tránh spam log
        if (_blockedIpCache.Contains(ipAddress))
        {
            return;
        }

        string ruleName = $"EDR Block {ipAddress}";
        _logger.LogWarning($"[FIREWALL] Action needed for IP: {ipAddress}");

        // 2. CHIẾN THUẬT: XÓA TRƯỚC - THÊM SAU
        // Luôn thử xóa rule cũ (hoặc rule trùng lặp) trước. 
        // Nếu rule chưa có thì lệnh này lỗi nhẹ, ta bỏ qua.
        RunNetshCommand($"advfirewall firewall delete rule name=\"{ruleName}\"");

        // 3. THÊM RULE MỚI (Chặn cả vào và ra)
        bool outSuccess =
            RunNetshCommand(
                $"advfirewall firewall add rule name=\"{ruleName}\" dir=out action=block remoteip={ipAddress}");
        bool inSuccess =
            RunNetshCommand(
                $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=block remoteip={ipAddress}");

        if (outSuccess && inSuccess)
        {
            _logger.LogInformation($"[FIREWALL SUCCESS] Blocked IP {ipAddress} (In/Out)");

            // 4. THÊM VÀO CACHE
            // Đánh dấu là đã chặn xong để vòng lặp sau không làm lại nữa
            _blockedIpCache.Add(ipAddress);
        }
        else
        {
            _logger.LogWarning($"[FIREWALL RETRY] Failed to apply rules for {ipAddress}. Will try again next cycle.");
            // Không thêm vào cache để lần sau thử lại
        }
    }

    // --- 3. UTILITIES ---
    // Function for run CMD safely
    private bool RunNetshCommand(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true, // Đọc kết quả trả về
                RedirectStandardError = true
            };

            using (var process = Process.Start(psi))
            {
                if (process == null) return false;

                // Chờ lệnh chạy xong (tối đa 5 giây để tránh treo)
                process.WaitForExit(5000);

                if (process.ExitCode == 0)
                {
                    return true; // Thành công
                }
                else
                {
                    if (!arguments.Contains("delete"))
                    {
                        var error = process.StandardOutput.ReadToEnd();
                        _logger.LogWarning($"Netsh warning/error. Args: {arguments}. Output: {error.Trim()}");
                    }

                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running netsh");
            return false;
        }
    }

    private bool CheckIfRuleExists(string ruleName)
    {
        // Lệnh 'show rule' sẽ trả về ExitCode = 0 nếu tìm thấy, và 1 nếu không tìm thấy
        return RunNetshCommand($"advfirewall firewall show rule name=\"{ruleName}\"");
    }
}
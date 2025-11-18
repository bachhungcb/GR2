using System;
using Microsoft.Extensions.Logging;
using MyWorkerService.Command;
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

    public HandleCommandService(ILogger<HandleCommandService> logger)
    {
        _logger = logger;
    }

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
                //TODO : add case 'BLOCK_IP' in the future

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

    // 2. BLOCK LOGIC
    private void HandleBlockProcess(string pidString)
    {
        _logger.LogInformation($"Received BLOCK command for PID: {pidString}");

        if (int.TryParse(pidString, out int pid))
        {
            try
            {
                try
                {
                    //Find the process
                    Process processToBlock = Process.GetProcessById(pid);
                    if (processToBlock != null)
                    {
                        //And KILL the process
                        processToBlock.Kill(true);
                        _logger.LogWarning($"[ACTION TAKEN]" +
                                           $"KILL process: {processToBlock.ProcessName} (PID: {pid})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[KILL FAILED]Process.Kill() when attempting to KILL Process: {pid}");
                }

                // CÁCH 2: Dùng "Búa Tạ" TASKKILL /F (Force)
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/F /PID {pid} /T", // /F=Force, /T=Tree (giết cả họ hàng)
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
                _logger.LogWarning($"[TASKKILL SENT] Executed taskkill /F /PID {pid}");
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
}
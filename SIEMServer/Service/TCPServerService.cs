using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyWorkerService.Services;
using MyWorkerService.Telemetry;
using SIEMServer.TCP;

namespace SIEMServer.Service;

public class TCPServerService : BackgroundService
{
    // 1. DI
    private readonly TCPServer _server;
    private readonly ILogger<WindowsBackgroundService> _logger;

    // 2. Constructor
    public TCPServerService(
        TCPServer server,
        ILogger<WindowsBackgroundService> logger)
    {
        _server = server;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // 3. Running server
            await _server.RunAsync();
        }
        catch (OperationCanceledException)
        {
            // When the stopping token is canceled, for example, a call made from services.msc,
            // we shouldn't exit with a non-zero exit code. In other words, this is expected...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);

            // Terminates this process and returns an exit code to the operating system.
            // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
            // performs one of two scenarios:
            // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
            // 2. When set to "StopHost": will cleanly stop the host, and log errors.
            //
            // In order for the Windows Service Management system to leverage configured
            // recovery options, we need to terminate the process with a non-zero exit code.
            Environment.Exit(1);
        }
    }
}
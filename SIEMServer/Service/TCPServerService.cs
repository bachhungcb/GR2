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

            // [SỬA ĐOẠN NÀY]
            // Đảm bảo log được ghi ra file TRƯỚC KHI thoát
            await Serilog.Log.CloseAndFlushAsync(); 
            
            Environment.Exit(1);
        }
    }
}
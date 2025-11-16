using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyWorkerService.Telemetry;

namespace MyWorkerService.Services;

public sealed class WindowsBackgroundService : BackgroundService
{
    // 1. DI 
    private readonly GetProcessService _processService;
    private readonly GetTCPConnectionsService _tcpConnectionsService;
    private readonly PostInfoService _postService;
    private readonly ILogger<WindowsBackgroundService> _logger;
    private readonly AgentIdService _agentIdService;

    // [MỚI] Thêm 2 dịch vụ
    private readonly LocalBlacklistService _localBlacklistService;
    private readonly HandleCommandService _commandHandler;

    // 2. Constructor
    public WindowsBackgroundService(
        GetProcessService processService,
        GetTCPConnectionsService tcpConnService,
        PostInfoService postService,
        ILogger<WindowsBackgroundService> logger,
        AgentIdService agentIdService,
        LocalBlacklistService localBlacklistService, // [MỚI]
        HandleCommandService commandHandler) // [MỚI]
    {
        _processService = processService;
        _tcpConnectionsService = tcpConnService;
        _postService = postService;
        _logger = logger;
        _agentIdService = agentIdService;
        _localBlacklistService = localBlacklistService; // [MỚI]
        _commandHandler = commandHandler; // [MỚI]
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 3. Get Agent Id
                var agentId = _agentIdService.GetAgentId();

                // [LOGIC MỚI]
                // 4. Lấy TẤT CẢ tiến trình
                var processes = _processService.GetAllProcessData();

                // 5. Lọc và Chặn (Nếu có)
                //    Hàm này sẽ tự gọi commandHandler.Kill()
                var alerts = _localBlacklistService.FilterAndBlock(processes, _commandHandler);

                // 6. Create new wrapper object
                var wrapper = new AgentTelemetry();

                // 7. Create a packet (Bao gồm cả các cảnh báo)
                var packet = wrapper.Wrap(
                    _processService, // (Sửa lại: bạn có thể truyền 'processes' đã lấy)
                    _tcpConnectionsService,
                    agentId,
                    alerts); // [SỬA] Truyền 'alerts' vào

                // 8. Send packet
                if (packet != null)
                {
                    await _postService.PostInformation(packet);
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
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
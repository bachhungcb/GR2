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

    // 2. Constructor
    public WindowsBackgroundService(
        GetProcessService processService,
        GetTCPConnectionsService tcpConnService,
        PostInfoService postService,
        ILogger<WindowsBackgroundService> logger,
        AgentIdService agentIdService
        )
    {
        _processService = processService;
        _tcpConnectionsService = tcpConnService;
        _postService = postService;
        _logger = logger;
        _agentIdService = agentIdService;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 3. Get Agent Id
                var agentId = _agentIdService.GetAgentId();
                
                // 3. Create new wrapper object
                var wrapper = new AgentTelemetry();
                
                // 4. Create a packet
                var packet = wrapper.Wrap(_processService, _tcpConnectionsService, agentId);

                // 5. Send packet
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
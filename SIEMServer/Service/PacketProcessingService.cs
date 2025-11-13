using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIEMServer.Interfaces;
using SIEMServer.Service.Channel;

namespace SIEMServer.Service;

/// <summary>
/// Dịch vụ Nền (Background Service) ⚙️ "Luồng Lạnh" (Cold Path) 🐢.
/// Nó chạy (runs) trên một luồng (thread) riêng biệt.
/// </summary>
public sealed class PacketProcessingService : BackgroundService
{
    private readonly ILogger<PacketProcessingService> _logger;
    private readonly PacketChannelService _channel;
    private readonly IServiceScopeFactory _scopeFactory;

    public PacketProcessingService(
        ILogger<PacketProcessingService> logger,
        PacketChannelService channel, // Queue
        IServiceScopeFactory scopeFactory) // factory
    {
        _logger = logger;
        _channel = channel;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Service (Kitchen) has been started");
        Console.WriteLine("[SERVICE] Has been started");
        //Wait until Queue has something
        try
        {
            // 1. Create an infinity loop outside
            // This will be stopped by 'stopping Token'
            while (!stoppingToken.IsCancellationRequested)
            {
                // 2. Wait effectively 
                // Code will sleep here
                // Until there IS data in queue
                await _channel.WaitForReadAsync(stoppingToken);
                
                // 3. Wake up and drain queue
                // Read everything
                // From queue as fast as possible
                while (_channel.TryRead(out RawPacket packet))
                {
                    try
                    {
                        //Create a new scope
                        //For this specific packet
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            //Request a new handler
                            var handler = scope.ServiceProvider
                                .GetRequiredService<IPacketHandlerService>();
                            
                            //Begin slow job
                            await handler.ProcessPacketAsync(
                                packet.JsonBuffer,
                                packet.AgentIp,
                                null); //TODO: fix SEND command later
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error (kitchen) while processing packet");
                    }
                } //End of 'TryRead' loop
            } //Go back to loop back 'WaitForReadAsync'
            Console.WriteLine("[SERVICE] Has ended");
        }
        catch (OperationCanceledException e)
        {
            _logger.LogInformation("Service (kitchen) cancelled");
        }
    }
}
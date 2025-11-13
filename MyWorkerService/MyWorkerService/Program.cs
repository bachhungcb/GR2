using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using MyWorkerService.Services;
using Serilog;

//Logger configure
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
    @"C:\temp\agent_log.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
try
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    //USING SERILOG
    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger);

    builder.Services.AddWindowsService(options =>
    {
        options.ServiceName = "MyProcessAgentService";
    });

    //Service 
    builder.Services.AddSingleton<GetProcessService>();
    builder.Services.AddSingleton<GetTCPConnectionsService>();
    builder.Services.AddSingleton<PostInfoService>();
    builder.Services.AddSingleton<AgentIdService>();
    builder.Services.AddSingleton<HandleCommandService>();
    

    //Background service
    builder.Services.AddHostedService<WindowsBackgroundService>();

    IHost host = builder.Build();
    host.Run();
}
catch(Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

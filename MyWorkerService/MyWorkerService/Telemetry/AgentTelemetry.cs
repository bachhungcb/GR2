using MyWorkerService.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;
using static MyWorkerService.Services.GetProcessService;
using static MyWorkerService.Services.GetTCPConnectionsService;
using MyWorkerService.Services;

namespace MyWorkerService.Telemetry
{

    public class AgentTelemetry
    {
        public Guid AgentId { get; set; } //For getting agentId
        public string HostName { get; set; }
        public List<ProcessJsonElement> Processes { get; set; }
        public List<TCPJsonElement> Connections { get; set; }
        public List<Alert> Alerts { get; set; } // [MỚI] Thêm trường cảnh báo

        public byte[]? Wrap(
            GetProcessService processService, 
            GetTCPConnectionsService tcpConnectionService,
            Guid agentId,
            List<Alert> alerts) // [SỬA] Thêm tham số 'alerts'
        {
            try
            {
                AgentId = agentId;
                HostName = System.Net.Dns.GetHostName();

                var processes = processService.GetAllProcessData();
                Processes = processes ?? new List<ProcessJsonElement>();

                var tcpConnections = tcpConnectionService.GetAllTCPConnection();
                Connections = tcpConnections ?? new List<TCPJsonElement>();

                Alerts = alerts; // [MỚI] Gán danh sách cảnh báo

                return JsonSerializer.SerializeToUtf8Bytes(this);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error at wrapping response: {ex.Message}");
                return null;
            }
        }
    }
}

using MyWorkerService.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static MyWorkerService.Services.GetProcessService;
using static MyWorkerService.Services.GetTCPConnectionsService;

namespace MyWorkerService.Telemetry
{

    public class AgentTelemetry
    {
        public Guid AgentId { get; set; } //For getting agentId
        public List<ProcessJsonElement> Processes { get; set; }
        public List<TCPJsonElement> Connections { get; set; }

        public byte[]? Wrap(
            GetProcessService processService, 
            GetTCPConnectionsService tcpConnectionService,
            Guid  agentId)
        {
            try
            {
                AgentId = agentId;
                
                var processes = processService.GetAllProcessData();
                Processes = processes ?? new List<ProcessJsonElement>();

                var tcpConnections = tcpConnectionService.GetAllTCPConnection();
                Connections = tcpConnections ?? new List<TCPJsonElement>();
                
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

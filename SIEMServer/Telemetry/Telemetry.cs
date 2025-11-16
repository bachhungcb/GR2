namespace SIEMServer.Telemetry
{
    public class Alert
    {
        public string ProcessName { get; set; }
        public int Pid { get; set; }
        public string MatchedRule { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class Telemetry
    {
        public Guid AgentId { get; set; }


        public struct ProcessJsonElement
        {
            public int Pid { get; set; }
            public string Name { get; set; }

            public string FilePath { get; set; }
            public string CommandLine { get; set; }
            
            public override string ToString() => $"{Name} ({Pid})";
        }

        public struct TCPJsonElement
        {
            public string LocalEndPointAddr { get; set; }
            public string RemoteEndPointAddr { get; set; }
            public string State { get; set; }

            public override string ToString() => $"{LocalEndPointAddr} -> {RemoteEndPointAddr} ";
        }

        public List<ProcessJsonElement> Processes { get; set; }
        public List<TCPJsonElement> Connections { get; set; }
        public List<Alert> Alerts { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string AgentIp { get; set; }
    }
}
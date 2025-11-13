namespace SIEMServer.Telemetry
{
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


    }
}

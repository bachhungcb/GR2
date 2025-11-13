using System.Net.NetworkInformation;
using System.Text.Json;

namespace MyWorkerService.Services
{
    public sealed class GetTCPConnectionsService
    {
        public struct TCPJsonElement
        {
            public TCPJsonElement(string _local, string _remote, string _state)
            {
                LocalEndPointAddr = _local;
                RemoteEndPointAddr = _remote;
                State = _state;
            }

            public string LocalEndPointAddr { get; set; }
            public string RemoteEndPointAddr { get; set; }
            public string State { get; set; }

            public override string ToString() => $"{LocalEndPointAddr} -> {RemoteEndPointAddr} ";
        }

        public List<TCPJsonElement>? GetAllTCPConnection()
        {
            try
            {
                List<TCPJsonElement> tCPJsonElements = new List<TCPJsonElement>();
                IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
                TcpConnectionInformation[] conn = properties.GetActiveTcpConnections();
                foreach (TcpConnectionInformation t in conn)
                {
                    var newJsonElement = new TCPJsonElement(
                        t.LocalEndPoint.Address.ToString(),
                        t.RemoteEndPoint.Address.ToString(),
                        t.State.ToString()
                        );
                    tCPJsonElements.Add(newJsonElement);
                }
                return tCPJsonElements;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetAllTCpConnection() error: {ex.Message}");
                return null;
            }

        }
    }
}

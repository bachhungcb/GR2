using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SIEMServer.TCP
{
    internal class TCPClient
    {
        public TCPClient() {
            SendAsyn().GetAwaiter().GetResult();
        }
        public async Task SendAsyn()
        {
            var hostName = Dns.GetHostName();
            IPHostEntry localhost = await Dns.GetHostEntryAsync(hostName);

            // Select the first IPv4 address from the host entry
            var ipAddress = localhost.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            if (ipAddress == null)
            {
                throw new InvalidOperationException("No IPv4 address found for the host.");
            }

            var ipEndPoint = new IPEndPoint(ipAddress, 13);

            using TcpClient client = new();
            await client.ConnectAsync(ipEndPoint);
            await using NetworkStream stream = client.GetStream();

            var buffer = new byte[1024];
            int received = await stream.ReadAsync(buffer);

            var message = Encoding.UTF8.GetString(buffer, 0, received);
            Console.WriteLine($"Message received: \"{message}\"");
            // Sample output:
            //     Message received: "📅 8/22/2022 9:07:17 AM 🕛"
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIEMServer.Model
{
    public class Agent
    {
        public Guid Id { get;set; }
        public required string HostName { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }

        public List<TelemetrySnapshots> Snapshots { get; set; }
    }
}

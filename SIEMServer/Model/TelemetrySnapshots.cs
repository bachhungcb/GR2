using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIEMServer.Model
{
    public class TelemetrySnapshots
    {
        public Guid Id { get; set; } //Snapshot ID
        public string AgentIpAddress { get; set; }
        
        public DateTime Timestamp { get; set; }

        //Foreign Key
        public Guid AgentId { get; set; }
        public Agent Agent {  get; set; }

        //Navigation for Entries
        public List<ProcessEntries> ProcessEntries { get; set; }
        public List<ConnectionEntries> ConnectionEntries { get; set; }
    }
}

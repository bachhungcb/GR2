using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIEMServer.Model
{
    public class ConnectionEntries
    {
        //Primary Key
        public Guid Id { get; set; }
        
        public string? LocalEndPointAddr { get; set; }
        public string? LocalEndPointName { get; set; }
        public string? RemoteEndPointAddr { get; set; }
        public string? RemoteEndPointName { get; set; }
        
        public string? State { get; set; }
        //Foreign key
        public Guid SnapshotId { get; set; }
        public TelemetrySnapshots Snapshot { get; set; }
    }
}

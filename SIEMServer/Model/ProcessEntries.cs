using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIEMServer.Model
{
    public class ProcessEntries
    {
        public Guid Id { get; set; }
        public int Pid { get; set; }
        public string Name { get; set; }
        
        public string FilePath { get; set; }
        public string Commandline { get; set; }
        
        
        public Guid SnapshotId { get; set; }
        public TelemetrySnapshots Snapshot { get; set; }
    }
}

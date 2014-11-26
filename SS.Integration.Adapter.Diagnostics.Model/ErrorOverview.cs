using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class ErrorOverview
    {
        public bool IsErrored { get; set; }
        public Exception Exception { get; set; }
        public DateTime ErroredAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int Sequence { get; set; }
    }
}

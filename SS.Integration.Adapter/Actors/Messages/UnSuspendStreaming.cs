using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class UnSuspendStreaming
    {
        public bool IsDatarefresh { get; set; }

        public string FixtureId { get; set; }

        public DateTime LastTimeStamp { get; set; }
    }
}

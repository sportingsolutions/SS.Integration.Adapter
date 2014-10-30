using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class FixtureOverview
    {
        public FixtureOverview()
        {
            TimeStamp = DateTime.UtcNow;
        }

        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public DateTime TimeStamp { get; private set; }

        public MatchStatus MatchStatus { get; internal set; }
        public int Sequence { get; internal set; }
        
        public bool IsStreaming { get; internal set; }
        public bool IsDeleted { get; internal set; }
        public bool IsErrored { get; internal set; }
        public bool IsOver { get; internal set; }
    }
}

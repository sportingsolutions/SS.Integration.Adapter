using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Actors.Messages
{
    public class UnSuspendRetryMessage
    {
        public FixtureState State { set; get; }
    }
}

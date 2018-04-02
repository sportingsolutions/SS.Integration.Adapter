using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class UpdateFixtureStateSuspendDelayedMsg
    {
        public string FixtureId { get; set; }

        public bool IsSuspendDelayedUpdate { get; set; }
    }
}

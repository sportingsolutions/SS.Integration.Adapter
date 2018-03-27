using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors.Messages
{
    public class SuspendMessage
    {

        public SuspensionReason SuspendReason { get; private set; }

        public SuspendMessage(SuspensionReason suspendReason)
        {
            SuspendReason = suspendReason;
        }
    }
}

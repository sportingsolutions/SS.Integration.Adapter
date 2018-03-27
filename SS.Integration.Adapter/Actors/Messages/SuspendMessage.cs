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
        public bool IsInvalidStream { get; private set; }

        public AddtionalSuspensionReasonInformation OtherSuspensionInfo { get; private set; }

        public SuspendMessage(AddtionalSuspensionReasonInformation otherSuspensionInfo = AddtionalSuspensionReasonInformation.Nothing)
        {
            OtherSuspensionInfo = otherSuspensionInfo;
        }
    }
}

using System;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class StreamListenerCreationFailedMsg
    {
        public IResourceFacade Resource { get; set; }

        public Exception Exception { get; set; }
    }
}

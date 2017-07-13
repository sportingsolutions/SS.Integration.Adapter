using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class StreamListenerCreationCompletedMsg
    {
        public IResourceFacade Resource { get; set; }
    }
}

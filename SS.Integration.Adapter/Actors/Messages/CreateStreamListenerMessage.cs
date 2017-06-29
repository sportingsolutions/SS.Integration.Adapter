using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class CreateStreamListenerMessage
    {
        internal IResourceFacade Resource { get; set; }
    }
}

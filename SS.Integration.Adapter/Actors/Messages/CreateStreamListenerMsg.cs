using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class CreateStreamListenerMsg
    {
        internal IResourceFacade Resource { get; set; }
    }
}

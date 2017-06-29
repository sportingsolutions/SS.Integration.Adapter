using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class ResourceStateUpdateMsg
    {
        public IResourceFacade Resource { get; set; }
    }
}

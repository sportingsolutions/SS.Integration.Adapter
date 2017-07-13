using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class ProcessResourceMsg
    {
        internal IResourceFacade Resource { get; set; }
    }
}

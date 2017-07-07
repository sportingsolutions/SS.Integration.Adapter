using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class StreamValidationMsg
    {
        public IResourceFacade Resource { get; set; }

        public StreamListenerActor.StreamListenerState State { get; set; }

        public int CurrentSequence { get; set; }
    }
}

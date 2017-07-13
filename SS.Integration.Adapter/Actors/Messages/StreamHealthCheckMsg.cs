namespace SS.Integration.Adapter.Actors.Messages
{
    internal class StreamHealthCheckMsg : ProcessResourceMsg
    {
        public Enums.StreamListenerState StreamingState { get; set; }
        public int CurrentSequence { get; set; }
    }
}

using System;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class StreamHealthCheckMsg
    {
        public string FixtureId { get; set; }

        public int Sequence { get; set; }

        public DateTime Received { get; set; }
    }
}

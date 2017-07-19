using System;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class UpdateStatsStartMsg
    {
        public bool IsSnapshot { get; set; }
        public DateTime UpdateReceivedAt { get; set; }
        public int Sequence { get; set; }
        public Fixture Fixture { get; set; }
    }
}

using System;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class UpdateStatsErrorMsg
    {
        public DateTime ErrorOccuredAt { get; set; }

        public Exception Error { get; set; }
    }
}

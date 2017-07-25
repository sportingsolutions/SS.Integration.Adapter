using System;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Actors.Messages
{
    public class UpdateSupervisorStateMsg
    {
        public string FixtureId { get; set; }

        public string Sport { get; set; }

        public int Epoch { get; set; }

        public int CurrentSequence { get; set; }

        public DateTime? StartTime { get; set; }

        public bool IsSnapshot { get; set; }

        public string CompetitionId { get; set; }

        public string CompetitionName { get; set; }

        public string Name { get; set; }

        public MatchStatus? MatchStatus { get; set; }

        public int[] LastEpochChangeReason { get; set; }

        public bool? IsStreaming { get; set; }

        public bool IsDeleted => MatchStatus.HasValue && MatchStatus.Value == Model.Enums.MatchStatus.Deleted;

        public bool? IsErrored { get; set; }

        public bool? IsSuspended { get; set; }

        public bool IsOver => MatchStatus.HasValue && MatchStatus.Value == Model.Enums.MatchStatus.MatchOver;

        public Exception Exception { get; set; }
    }
}

using System;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Diagnostics.Model.Interface
{
    public interface IFixtureOverviewDelta
    {
        string Id { get; }
        int? Sequence { get; }
        int? Epoch { get; }
        bool? IsStreaming { get; }
        bool? IsDeleted { get; }
        bool? IsErrored { get; }
        bool? IsSuspended { get; }
        bool? IsOver { get; }
        DateTime? StartTime { get;  }

        MatchStatus? MatchStatus { get; }

        ErrorOverview LastError { get; }
        FeedUpdateOverview FeedUpdate { get; }
    }
}
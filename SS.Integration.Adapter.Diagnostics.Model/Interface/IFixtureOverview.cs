using System;
using System.Collections.Generic;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Diagnostics.Model.Interface
{
    public interface IFixtureOverview : IFixtureOverviewDelta
    {
        string Name { get; set; }
        string Sport { get; }

        string CompetitionId { get; }
        string CompetitionName { get; }
        
        MatchStatus? MatchStatus { get; set; }
        DateTime TimeStamp { get; }
        
        IEnumerable<ErrorOverview> GetErrorsAudit(int limit = 0);
        IEnumerable<FeedUpdateOverview> GetFeedAudit(int limit = 0);
    }
}
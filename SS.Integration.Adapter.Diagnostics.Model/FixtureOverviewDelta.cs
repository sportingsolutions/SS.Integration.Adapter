using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class FixtureOverviewDelta : IFixtureOverviewDelta
    {
        public string Id { get; internal set; }
        public int? Sequence { get; internal set; }
        public int? Epoch { get; internal set; }
        public bool? IsStreaming { get; internal set; }
        public bool? IsDeleted { get; internal set; }
        public bool? IsErrored { get; internal set; }
        public bool? IsSuspended { get; internal set; }
        public bool? IsOver { get; internal set; }
        public DateTime? StartTime { get; internal set; }
        public MatchStatus? MatchStatus { get; internal set; }
        public ErrorOverview LastError { get; internal set; }
        public FeedUpdateOverview FeedUpdate { get; internal set; }
    }
}

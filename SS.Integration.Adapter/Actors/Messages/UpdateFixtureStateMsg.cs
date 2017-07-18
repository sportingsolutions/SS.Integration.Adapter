using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class UpdateFixtureStateMsg
    {
        public string FixtureId { get; set; }

        public string Sport { get; set; }

        public MatchStatus Status { get; set; }

        public int Sequence { get; set; }

        public int Epoch { get; set; }
    }
}

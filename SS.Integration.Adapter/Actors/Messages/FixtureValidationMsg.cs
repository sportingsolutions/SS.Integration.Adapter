using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Actors.Messages
{
    internal class FixtureValidationMsg
    {
        public Fixture FixtureDetla { get; set; }

        public int Sequence { get; set; }

        public int Epoch { get; set; }

        public bool IsSequenceValid { get; set; }

        public bool IsEpochValid { get; set; }
    }
}

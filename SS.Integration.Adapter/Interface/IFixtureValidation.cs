using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Interface
{
    public interface IFixtureValidation
    {
        bool IsNotMissedUpdates(Fixture fixtureDelta, int sequence);

	    bool IsSequnceActual(Fixture fixtureDelta, int sequence);

		bool IsEpochValid(Fixture fixtureDelta, int epoch);

        bool IsSnapshotNeeded(IResourceFacade resourceFacade, FixtureState state);
    }
}

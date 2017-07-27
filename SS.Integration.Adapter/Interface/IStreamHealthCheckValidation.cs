using System;
using SS.Integration.Adapter.Enums;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Interface
{
    public interface IStreamHealthCheckValidation
    {
        bool ValidateStream(IResourceFacade resource, StreamListenerState state, int sequence);

        bool CanConnectToStreamServer(IResourceFacade resource, StreamListenerState state);

        bool ShouldSuspendOnDisconnection(FixtureState fixtureState, DateTime? fixtureStartTime);
    }
}

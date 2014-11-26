using System;
using System.Collections.Generic;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Diagnostics.Model.Interface
{
    public interface ISupervisor : IStreamListenerManager, IDisposable
    {
        ISupervisorProxy Proxy { get; }

        void ForceSnapshot(string fixtureId);
        IObservable<IFixtureOverviewDelta> GetFixtureOverviewStream();
        IEnumerable<IFixtureOverview> GetFixtures();
        IFixtureOverview GetFixtureOverview(string fixtureId);

        void RemoveFixtureEventState(string fixtureId);

        void Initialise();
    }
}

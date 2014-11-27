using System;
using System.Collections.Generic;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Diagnostics.Model.Interface
{
    public interface ISupervisor : IStreamListenerManager, IDisposable
    {
        ISupervisorProxy Proxy { get; }

        
        IObservable<IFixtureOverviewDelta> GetFixtureOverviewStream(string fixtureId);
        IEnumerable<IFixtureOverview> GetFixtures();
        IFixtureOverview GetFixtureOverview(string fixtureId);

        IEnumerable<ISportOverview> GetSports();
        ISportOverview GetSportOverview(string sportCode);
        IObservable<ISportOverview> GetSportOverviewStream(string sportCode);

        IAdapterVersion GetAdapterVersion();


        void RemoveFixtureEventState(string fixtureId);
        void ForceSnapshot(string fixtureId);

        void Initialise();
    }
}

using System;
using System.Collections.Generic;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Diagnostics.Model.Interface
{
    public interface ISupervisor : IStreamListenerManager, IDisposable
    {
        ISupervisorProxy Proxy { get; }
        ISupervisorService Service { get; }
        
        IObservable<IFixtureOverviewDelta> GetFixtureOverviewStream(string fixtureId);
        IObservable<IFixtureOverviewDelta> GetAllFixtureOverviewStreams();
        IEnumerable<IFixtureOverview> GetFixtures();
        IObservable<IFixtureOverviewDelta> GetFixtureStreams();
        IFixtureOverview GetFixtureOverview(string fixtureId);

        IEnumerable<ISportOverview> GetSports();
        ISportOverview GetSportOverview(string sportCode);
        IObservable<ISportOverview> GetSportOverviewStream(string sportCode);
        IObservable<ISportOverview> GetAllSportOverviewStreams();

        IAdapterVersion GetAdapterVersion();


        void RemoveFixtureState(string fixtureId);
        void ForceSnapshot(string fixtureId);
        void RestartListener(string fixtureId);

        void Initialise();
    }
}

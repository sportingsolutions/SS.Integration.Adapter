using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Diagnostics.Actors.Messages;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Model.Enums;
using ServiceInterface = SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using ServiceModel = SS.Integration.Adapter.Diagnostics.Model.Service.Model;
using ServiceModelInterface = SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;

namespace SS.Integration.Adapter.Diagnostics
{
    public class SupervisorProxy : ServiceInterface.ISupervisorProxy
    {
        #region Fields

        private readonly IActorRef _supervisorActor;

        #endregion

        #region Constructors

        public SupervisorProxy(IActorRef supervisorActor)
        {
            _supervisorActor = supervisorActor ?? throw new ArgumentNullException(nameof(supervisorActor));
        }

        #endregion

        #region Imlementation of ISupervisorProxy

        public IEnumerable<ServiceModelInterface.ISportOverview> GetSports()
        {
            var getSportsMsg = new GetSportsMsg();
            var sportsOverview = _supervisorActor.Ask<IEnumerable<SportOverview>>(getSportsMsg).Result;
            return sportsOverview.Any()
                ? sportsOverview.Select(sport => sport.ToServiceModel<ServiceModel.SportOverview>())
                : Enumerable.Empty<ServiceModelInterface.ISportOverview>();
        }

        public ServiceModelInterface.ISportDetails GetSportDetail(string sportCode)
        {
            var getSportOverviewMsg = new GetSportOverviewMsg
            {
                SportCode = sportCode
            };
            var overview = string.IsNullOrWhiteSpace(sportCode)
                ? null
                : _supervisorActor.Ask<SportOverview>(getSportOverviewMsg).Result;

            var details = overview?.ToServiceModel();

            if (details == null)
            {
                return null;
            }

            var getFixturesMsg = new GetFixturesMsg();
            var sportFixtures = _supervisorActor.Ask<IEnumerable<FixtureOverview>>(getFixturesMsg).Result
                .Where(f => f.Sport.Equals(sportCode) &&
                            !(f.ListenerOverview.IsDeleted.GetValueOrDefault() ||
                              f.ListenerOverview.MatchStatus.HasValue &&
                              (int)f.ListenerOverview.MatchStatus.Value >= (int)MatchStatus.MatchOverUnConfirmed));

            foreach (var fixture in sportFixtures)
            {
                details.AddFixture(fixture.ToServiceModel<ServiceModel.FixtureOverview>());
            }


            return details;
        }

        public ServiceModelInterface.IFixtureDetails GetFixtureDetail(string fixtureId)
        {
            var getFixtureOverviewMsg = new GetFixtureOverviewMsg
            {
                FixtureId = fixtureId
            };
            var overview = string.IsNullOrWhiteSpace(fixtureId)
                ? null
                : _supervisorActor.Ask<FixtureOverview>(getFixtureOverviewMsg).Result;

            var details = overview?.ToServiceModel();

            if (details == null)
            {
                return null;
            }

            foreach (var update in overview.GetFeedAudit())
            {
                details.AddProcessingEntry(update.ToFixtureProcessingEntryServiceModel());
            }

            return details;
        }

        public ServiceModelInterface.IAdapterStatus GetAdapterStatus()
        {
            var getAdapterStatusMsg = new GetAdapterStatusMsg();
            return _supervisorActor.Ask<ServiceModel.AdapterStatus>(getAdapterStatusMsg).Result;
        }

        public IEnumerable<ServiceModelInterface.IFixtureProcessingEntry> GetFixtureHistory(string fixtureId)
        {
            var getFixtureOverviewMsg = new GetFixtureOverviewMsg
            {
                FixtureId = fixtureId
            };
            var overview = string.IsNullOrWhiteSpace(fixtureId)
                ? null
                : _supervisorActor.Ask<FixtureOverview>(getFixtureOverviewMsg).Result;

            if (overview == null)
            {
                return Enumerable.Empty<ServiceModelInterface.IFixtureProcessingEntry>();
            }

            var ret = new List<ServiceModelInterface.IFixtureProcessingEntry>();

            foreach (var update in overview.GetFeedAudit())
            {
                ret.Add(update.ToFixtureProcessingEntryServiceModel());
            }

            return ret;
        }

        public IEnumerable<ServiceModelInterface.IFixtureOverview> GetFixtures()
        {
            var getFixturesMsg = new GetFixturesMsg();
            var fixtures = _supervisorActor.Ask<IEnumerable<FixtureOverview>>(getFixturesMsg).Result
                .Where(f => !(f.ListenerOverview.IsDeleted.GetValueOrDefault() ||
                              f.ListenerOverview.MatchStatus.HasValue && (int)f.ListenerOverview.MatchStatus <= (int)MatchStatus.MatchOverUnConfirmed));
            return fixtures.Any()
                ? fixtures.Select(f => f.ToServiceModel<ServiceModel.FixtureOverview>())
                : Enumerable.Empty<ServiceModel.FixtureOverview>();
        }

        public bool TakeSnapshot(string fixtureId)
        {
            var takeSnapshotMsg = new TakeSnapshotMsg
            {
                FixtureId = fixtureId
            };
            _supervisorActor.Tell(takeSnapshotMsg);

            return true;
        }

        public bool RestartListener(string fixtureId)
        {
            var restartStreamListenerMsg = new RestartStreamListenerMsg
            {
                FixtureId = fixtureId
            };
            _supervisorActor.Tell(restartStreamListenerMsg);

            return true;
        }

        public bool ClearState(string fixtureId)
        {
            var clearFixtureStateMsg = new ClearFixtureStateMsg
            {
                FixtureId = fixtureId
            };
            _supervisorActor.Tell(clearFixtureStateMsg);

            return true;
        }

        #endregion

        #region Imlementation of IDisposable

        public void Dispose()
        {
        }

        #endregion
    }
}

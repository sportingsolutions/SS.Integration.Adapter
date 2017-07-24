using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using ServiceModelInterface = SS.Integration.Adapter.Diagnostics.Model.Service.Interface;

namespace SS.Integration.Adapter.Diagnostics.Actors
{
    public class SupervisorActor : ReceiveActor
    {
        #region Constants

        public const string ActorName = nameof(SupervisorActor);
        public const string Path = "/user/" + ActorName;

        #endregion

        #region Private members

        private readonly ILog _logger = LogManager.GetLogger(typeof(SupervisorActor).ToString());
        private readonly ServiceModelInterface.ISupervisorStreamingService _streamingService;
        private readonly IObjectProvider<Dictionary<string, FixtureOverview>> _objectProvider;
        private readonly Dictionary<string, SportOverview> _sportOverviews;
        private readonly Dictionary<string, FixtureOverview> _fixtures;

        #endregion

        #region Constructors

        public SupervisorActor(
            ServiceModelInterface.ISupervisorStreamingService streamingService,
            IObjectProvider<Dictionary<string, FixtureOverview>> objectProvider)
        {
            _streamingService = streamingService ?? throw new ArgumentNullException(nameof(streamingService));
            _objectProvider = objectProvider ?? throw new ArgumentNullException(nameof(objectProvider));

            Dictionary<string, FixtureOverview> storedObject;
            TryLoadState(out storedObject);

            _sportOverviews = new Dictionary<string, SportOverview>();
            _fixtures = storedObject != null
                ? new Dictionary<string, FixtureOverview>(storedObject)
                : new Dictionary<string, FixtureOverview>();

            SetupSports();

            Receive<UpdateSupervisorStateMsg>(msg => UpdateSupervisorStateMsgHandler(msg));
        }

        #endregion

        #region Message Handlers

        private void UpdateSupervisorStateMsgHandler(UpdateSupervisorStateMsg msg)
        {
        }

        #endregion

        #region Private methods

        private void TryLoadState(out Dictionary<string, FixtureOverview> storedObject)
        {
            storedObject = null;

            try
            {
                storedObject = _objectProvider.GetObject(null);
            }
            catch (Exception ex)
            {
                _logger.Error("Error while loading Supervisor state: {0}", ex);
            }
        }

        private void SetupSports()
        {
            foreach (var sportGroup in _fixtures.Values.GroupBy(f => f.Sport))
            {
                UpdateSportDetails(sportGroup.Key);
            }

        }

        private void UpdateSportDetails(string sportName)
        {
            var sportOverview = new SportOverview { Name = sportName };

            var fixturesForSport = _fixtures.Values.Where(f =>
                    f.Sport == sportOverview.Name
                    && (f.ListenerOverview.IsDeleted.HasValue && !f.ListenerOverview.IsDeleted.Value || !f.ListenerOverview.IsDeleted.HasValue))
                .ToList();
            sportOverview.Total = fixturesForSport.Count;
            sportOverview.InErrorState = fixturesForSport.Count(f => f.ListenerOverview.IsErrored.HasValue && f.ListenerOverview.IsErrored.Value);

            var groupedByMatchStatus = fixturesForSport
                .GroupBy(f => f.ListenerOverview.MatchStatus, f => f.ListenerOverview.MatchStatus)
                .Where(g => g.Key.HasValue).ToDictionary(g => g.Key.Value, g => g.Count());

            if (groupedByMatchStatus.Any())
            {

                sportOverview.InPlay = groupedByMatchStatus.ContainsKey(MatchStatus.InRunning)
                    ? groupedByMatchStatus[MatchStatus.InRunning]
                    : 0;

                sportOverview.InPreMatch = groupedByMatchStatus.ContainsKey(MatchStatus.Prematch)
                    ? groupedByMatchStatus[MatchStatus.Prematch]
                    : 0;

                sportOverview.InSetup = groupedByMatchStatus.ContainsKey(MatchStatus.Setup)
                    ? groupedByMatchStatus[MatchStatus.Setup]
                    : 0;
            }

            if (_sportOverviews.ContainsKey(sportOverview.Name) &&
                _sportOverviews[sportOverview.Name].Equals(sportOverview))
            {
                return;
            }

            _sportOverviews[sportOverview.Name] = sportOverview;

            _streamingService.OnSportUpdate(sportOverview.ToServiceModel());
        }

        private IFixtureOverview GetFixtureOverview(string fixtureId)
        {
            FixtureOverview fixtureOverview;
            return _fixtures.TryGetValue(fixtureId, out fixtureOverview)
                ? fixtureOverview
                : _fixtures[fixtureId] = new FixtureOverview(fixtureId);
        }

        #endregion
    }
}

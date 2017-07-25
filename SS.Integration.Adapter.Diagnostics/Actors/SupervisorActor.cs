using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Diagnostics.Actors.Messages;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using ServiceInterface = SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using ServiceModelInterface = SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;
using ServiceModel = SS.Integration.Adapter.Diagnostics.Model.Service.Model;

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
        private readonly ServiceInterface.ISupervisorStreamingService _streamingService;
        private readonly IObjectProvider<Dictionary<string, FixtureOverview>> _objectProvider;
        private readonly Dictionary<string, SportOverview> _sportsOverview;
        private readonly Dictionary<string, FixtureOverview> _fixturesOverview;

        #endregion

        #region Constructors

        public SupervisorActor(
            ServiceInterface.ISupervisorStreamingService streamingService,
            IObjectProvider<Dictionary<string, FixtureOverview>> objectProvider)
        {
            _streamingService = streamingService ?? throw new ArgumentNullException(nameof(streamingService));
            _objectProvider = objectProvider ?? throw new ArgumentNullException(nameof(objectProvider));

            Dictionary<string, FixtureOverview> storedObject;
            TryLoadState(out storedObject);

            _sportsOverview = new Dictionary<string, SportOverview>();
            _fixturesOverview = storedObject != null
                ? new Dictionary<string, FixtureOverview>(storedObject)
                : new Dictionary<string, FixtureOverview>();

            SetupSports();

            Receive<UpdateSupervisorStateMsg>(msg => UpdateSupervisorStateMsgHandler(msg));
            Receive<UpdateAdapterStatusMsg>(msg => UpdateAdapterStatusMsgHandler(msg));
            Receive<GetAdapterStatusMsg>(msg => GetAdapterStatusMsgHandler(msg));
            Receive<GetSportsMsg>(msg => GetSportsMsgHandler(msg));
            Receive<GetSportOverviewMsg>(msg => GetSportOverviewMsgHandler(msg));
            Receive<GetFixturesMsg>(msg => GetFixturesMsgHandler(msg));
            Receive<GetFixtureOverviewMsg>(msg => GetFixtureOverviewMsgHandler(msg));

            Context.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(60),
                Self,
                new UpdateAdapterStatusMsg(),
                Self);
        }

        #endregion

        #region Message Handlers

        private void UpdateSupervisorStateMsgHandler(UpdateSupervisorStateMsg msg)
        {
            var fixtureOverview = GetFixtureOverview(msg.FixtureId);
            ServiceModel.FixtureDetails details = fixtureOverview.ToServiceModel();
            details.Id = msg.FixtureId;
            details.IsDeleted = msg.IsDeleted;
            details.IsOver = msg.IsOver;

            _streamingService.OnFixtureUpdate(details);

            if (msg.IsErrored.HasValue && msg.IsErrored.Value && msg.Exception != null)
            {
                var error = new ServiceModel.ProcessingEntryError
                {
                    Timestamp = DateTime.UtcNow,
                    Message = msg.Exception.Message,
                    FixtureId = msg.FixtureId,
                    FixtureDescription = fixtureOverview.Name,
                    Sequence = msg.CurrentSequence
                };

                _streamingService.OnError(error);
            }

            UpdateSportDetails(msg.Sport);
        }

        private void UpdateAdapterStatusMsgHandler(UpdateAdapterStatusMsg msg)
        {
            var status = GetAdapterStatus();
            _streamingService.OnAdapterUpdate(status);
        }

        private void GetAdapterStatusMsgHandler(GetAdapterStatusMsg msg)
        {
            Sender.Tell(GetAdapterStatus());
        }

        private void GetSportsMsgHandler(GetSportsMsg msg)
        {
            Sender.Tell(_sportsOverview.Values);
        }

        private void GetSportOverviewMsgHandler(GetSportOverviewMsg msg)
        {
            Sender.Tell(msg.SportCode != null && _sportsOverview.ContainsKey(msg.SportCode)
                ? _sportsOverview[msg.SportCode]
                : null);
        }

        private void GetFixturesMsgHandler(GetFixturesMsg msg)
        {
            Sender.Tell(_fixturesOverview.Values);
        }

        private void GetFixtureOverviewMsgHandler(GetFixtureOverviewMsg msg)
        {
            Sender.Tell(GetFixtureOverview(msg.FixtureId));
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
            foreach (var sportGroup in _fixturesOverview.Values.GroupBy(f => f.Sport))
            {
                UpdateSportDetails(sportGroup.Key);
            }
        }

        private void UpdateSportDetails(string sportName)
        {
            var sportOverview = new SportOverview { Name = sportName };

            var fixturesForSport = _fixturesOverview.Values.Where(f =>
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

            if (_sportsOverview.ContainsKey(sportOverview.Name) &&
                _sportsOverview[sportOverview.Name].Equals(sportOverview))
            {
                return;
            }

            _sportsOverview[sportOverview.Name] = sportOverview;

            _streamingService.OnSportUpdate(sportOverview.ToServiceModel());
        }

        private FixtureOverview GetFixtureOverview(string fixtureId)
        {
            FixtureOverview fixtureOverview;
            return _fixturesOverview.TryGetValue(fixtureId, out fixtureOverview)
                ? fixtureOverview
                : _fixturesOverview[fixtureId] = new FixtureOverview(fixtureId);
        }

        private ServiceModelInterface.IAdapterStatus GetAdapterStatus()
        {
            var adapterVersionInfo = AdapterVersionInfo.GetAdapterVersionInfo();

            return new ServiceModel.AdapterStatus
            {
                AdapterVersion = adapterVersionInfo.AdapterVersion,
                PluginName = adapterVersionInfo.PluginName,
                PluginVersion = adapterVersionInfo.PluginVersion,
                UdapiSDKVersion = adapterVersionInfo.UdapiSDKVersion,
                MemoryUsage = GC.GetTotalMemory(false).ToString(),
                RunningThreads = Process.GetCurrentProcess().Threads.Count.ToString()
            };
        }

        #endregion

        #region Private messages

        private class UpdateAdapterStatusMsg
        {   
        }

        #endregion
    }
}

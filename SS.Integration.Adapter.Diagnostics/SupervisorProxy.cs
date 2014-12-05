//Copyright 2014 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Diagnostics.Model.Service.Model;
using SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;


namespace SS.Integration.Adapter.Diagnostics
{
    public class SupervisorProxy : ISupervisorProxy
    {
        private const int ADAPTER_STATUS_UPDATE_INTERVAL_SECONDS = 60;
        

        public SupervisorProxy(ISupervisor supervisor)
        {
            if (supervisor == null)
                throw new ArgumentNullException("supervisor");

            Supervisor = supervisor;

            Init();
        }

        public ISupervisor Supervisor { get; private set; }

        private void Init()
        {
            // adapter status is sent out every ADAPTER_STATUS_UPDATE_TIMEOUT_SECONDS
            Observable.Interval(TimeSpan.FromSeconds(ADAPTER_STATUS_UPDATE_INTERVAL_SECONDS), ThreadPoolScheduler.Instance).Subscribe(OnAdapterStatusChanged);

            Supervisor.GetAllSportOverviewStreams().ObserveOn(ThreadPoolScheduler.Instance).Subscribe(OnSportUpdate);
            Supervisor.GetAllFixtureOverviewStreams().ObserveOn(ThreadPoolScheduler.Instance).Subscribe(OnFixtureUpdate);
        }

        #region ISupervisorProxy

        public IEnumerable<Model.Service.Model.Interface.ISportOverview> GetSports()
        {
            List<Model.Service.Model.Interface.ISportOverview> sports = new List<Model.Service.Model.Interface.ISportOverview>();

            foreach (var sport in Supervisor.GetSports())
            {
                Model.Service.Model.SportOverview sp = new Model.Service.Model.SportOverview();
                FillSportOverview(sp, sport);
                sports.Add(sp);
            }

            return sports;
        }

        public ISportDetails GetSportDetail(string sportCode)
        {
            if (string.IsNullOrEmpty(sportCode))
                return null;

            Model.Interface.ISportOverview overview = Supervisor.GetSportOverview(sportCode);
            if (overview == null)
                return null;

            SportDetails details = new SportDetails();
            FillSportOverview(details, overview);

            foreach(var fixture in Supervisor.GetFixtures())
            {
                if(string.Equals(fixture.Sport, sportCode))
                {
                    // do not include deleted or matchover fixtures
                    if (fixture.ListenerOverview.IsDeleted.GetValueOrDefault()  ||
                        (fixture.ListenerOverview.MatchStatus.HasValue && (int)fixture.ListenerOverview.MatchStatus.Value >= (int)Integration.Adapter.Model.Enums.MatchStatus.MatchOverUnConfirmed))
                        continue;

                    details.AddFixture(CreateFixtureOverview(fixture));
                }
            }

            return details;
        }

        public IFixtureDetails GetFixtureDetail(string fixtureId)
        {
            if(string.IsNullOrEmpty(fixtureId))
                return null;

            var overview = Supervisor.GetFixtureOverview(fixtureId);

            FixtureDetails details = new FixtureDetails();
            FillFixtureOverview(details, overview);

            foreach(var update in overview.GetFeedAudit())
            {
                FixtureProcessingEntry entry = new FixtureProcessingEntry();
                FillProcessingEntry(entry, update);
                details.AddProcessingEntry(entry);
            }

            return details;
        }

        public IAdapterStatus GetAdapterStatus()
        {
            var status = Supervisor.GetAdapterVersion();

            return new AdapterStatus
            {
                AdapterVersion = "Test", //status.AdapterVersion,
                PluginName = "Test", //status.PluginName,
                PluginVersion = "Test", //status.PluginVersion,
                UdapiSDKVersion = "Test", // status.UdapiSDKVersion,
                MemoryUsage = GC.GetTotalMemory(false).ToString(),
                RunningThreads = Process.GetCurrentProcess().Threads.Count.ToString()
            };
        }

        public IEnumerable<IFixtureProcessingEntry> GetFixtureHistory(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return Enumerable.Empty<IFixtureProcessingEntry>();

            List<IFixtureProcessingEntry> ret = new List<IFixtureProcessingEntry>();

            var tmp = Supervisor.GetFixtureOverview(fixtureId);
            foreach(var update in tmp.GetFeedAudit())
            {
                FixtureProcessingEntry entry = new FixtureProcessingEntry();
                FillProcessingEntry(entry, update);
                ret.Add(entry);
            }

            return ret;
        }

        public IEnumerable<Model.Service.Model.Interface.IFixtureOverview> GetFixtures()
        {
            return Supervisor.GetFixtures()
                .Where(x => x.ListenerOverview.IsDeleted.GetValueOrDefault() == false && (x.ListenerOverview.MatchStatus.HasValue && (int)x.ListenerOverview.MatchStatus <= (int)Integration.Adapter.Model.Enums.MatchStatus.MatchOverUnConfirmed))
                .Select(CreateFixtureOverview).ToList();
        }

        public void Dispose()
        {
        }

        #endregion

        #region Push-notifications

        private void OnAdapterStatusChanged(long t)
        {
            var status = GetAdapterStatus();
            Supervisor.Service.StreamingService.OnAdapterUpdate(status);
        }

        private void OnSportUpdate(Model.Interface.ISportOverview sport)
        {
            if (sport == null)
                return;

            SportDetails details = new SportDetails();
            // we don't send out the entire list of fixtures as the 
            // amount of data would be too big
            FillSportOverview(details, sport);
            Supervisor.Service.StreamingService.OnSportUpdate(details);
        }

        private void OnFixtureUpdate(IFixtureOverviewDelta fixture)
        {
            if (fixture == null)
                return;

            FixtureDetails details = new FixtureDetails {Id = fixture.Id};

            // ListenerOverview is not suitable to use here....
            // we have to get the FixtureOverview to have all data
            var overview = Supervisor.GetFixtureOverview(fixture.Id);
            FillFixtureOverview(details, overview);

            if (fixture.ListenerOverview != null)
            {
                details.IsDeleted = fixture.ListenerOverview.IsDeleted.GetValueOrDefault();
                details.IsOver = fixture.ListenerOverview.IsOver.GetValueOrDefault();
            }

            if(fixture.FeedUpdate != null)
            {
                FixtureProcessingEntry entry = new FixtureProcessingEntry();
                FillProcessingEntry(entry, fixture.FeedUpdate);
                details.AddProcessingEntry(entry);
            }

            Supervisor.Service.StreamingService.OnFixtureUpdate(details);

            if(fixture.LastError != null && fixture.LastError.IsErrored)
            {
                ProcessingEntryError error = new ProcessingEntryError
                {
                    FixtureId = fixture.Id,
                    FixtureDescription = "TEST", //fixture.Name,
                    Sequence = fixture.ListenerOverview.Sequence.HasValue ? fixture.ListenerOverview.Sequence.Value : -1
                };

                FillProcessingEntryError(error, fixture.LastError);
                Supervisor.Service.StreamingService.OnError(error);
            }

        }

        #endregion

        private static Model.Service.Model.Interface.IFixtureOverview CreateFixtureOverview(Model.Interface.IFixtureOverview fixture)
        {
            Model.Service.Model.FixtureOverview ret = new Model.Service.Model.FixtureOverview();
            FillFixtureOverview(ret, fixture);
            return ret;
        }

        private static void FillSportOverview(Model.Service.Model.SportOverview to, Model.Interface.ISportOverview from)
        {
            to.Name = from.Name;
            to.InErrorState = from.InErrorState;
            to.InPlay = from.InPlay;
            to.InPreMatch = from.InPreMatch;
            to.InSetup = from.InSetup;
            to.Total = from.Total;
        }

        private static void FillFixtureOverview(Model.Service.Model.FixtureOverview to, Model.Interface.IFixtureOverview from)
        {
            to.Id = from.Id;
            to.IsStreaming = from.ListenerOverview.IsStreaming.GetValueOrDefault();
            to.IsInErrorState = from.ListenerOverview.IsErrored.GetValueOrDefault();
            to.StartTime = from.ListenerOverview.StartTime.GetValueOrDefault();
            to.Competition = from.CompetitionName;
            to.CompetitionId = from.CompetitionId;
            to.Description = from.Name;

            if (from.ListenerOverview.MatchStatus.HasValue)
            {
                switch (from.ListenerOverview.MatchStatus)
                {
                    case Integration.Adapter.Model.Enums.MatchStatus.InRunning:
                        to.State = FixtureState.Running;
                        break;
                    case Integration.Adapter.Model.Enums.MatchStatus.MatchOver:
                        to.State = FixtureState.Over;
                        break;
                    case Integration.Adapter.Model.Enums.MatchStatus.Prematch:
                        to.State = FixtureState.PreMatch;
                        break;
                    case Integration.Adapter.Model.Enums.MatchStatus.Ready:
                        to.State = FixtureState.Ready;
                        break;
                    case Integration.Adapter.Model.Enums.MatchStatus.Setup:
                        to.State = FixtureState.Setup;
                        break;
                }
            }
        }

        private static void FillProcessingEntry(FixtureProcessingEntry entry, FeedUpdateOverview update)
        {
            //entry.Epoch
            //entry.EpochChangeReasons
            //entry.Exception
            entry.IsUpdate = !update.IsSnapshot;
            entry.Sequence = update.Sequence.ToString();
            entry.Timestamp = update.Issued;
            entry.State = update.IsProcessed ? FixtureProcessingState.PROCESSED : FixtureProcessingState.PROCESSING;
        }

        private static void FillProcessingEntryError(ProcessingEntryError to, ErrorOverview from)
        { 
            to.Message = from.Exception != null ? from.Exception.Message : "Unknown";
            to.Timestamp = from.ErroredAt;
        }

        public bool TakeSnapshot(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return false;

            try
            {
                Supervisor.ForceSnapshot(fixtureId);
                return true;
            }
            catch { }

            return false;
        }

        public bool RestartListener(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return false;

            try
            {
                Supervisor.ForcetListenerStop(fixtureId);
                return true;
            }
            catch{ }

            return false;
        }

        public bool ClearState(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return false;

            try
            {
                Supervisor.RemoveFixtureState(fixtureId);
                return true;
            }
            catch { }
            return false;
        }
    }
}

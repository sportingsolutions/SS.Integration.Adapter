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
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Diagnostics.Model.Service.Model;
using SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;
using SS.Integration.Adapter.Model.Enums;
using FixtureOverview = SS.Integration.Adapter.Diagnostics.Model.Service.Model.FixtureOverview;
using IFixtureOverview = SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface.IFixtureOverview;
using SportOverview = SS.Integration.Adapter.Diagnostics.Model.Service.Model.SportOverview;

namespace SS.Integration.Adapter.Diagnostics
{
    public class SupervisorProxy : ISupervisorProxy
    {

        public SupervisorProxy(ISupervisor supervisor)
        {
            if (supervisor == null)
                throw new ArgumentNullException("supervisor");

            Supervisor = supervisor;
        }

        public ISupervisor Supervisor { get; private set; }

        public IEnumerable<Model.Service.Model.Interface.ISportOverview> GetSports()
        {
            Dictionary<string, Model.Service.Model.Interface.ISportOverview> sports = new Dictionary<string, Model.Service.Model.Interface.ISportOverview>();
            
            foreach(var fixture in Supervisor.GetFixtures())
            {
                Model.Service.Model.Interface.ISportOverview sportoverview = null;
                if (!sports.ContainsKey(fixture.Sport))
                {
                    sportoverview = new SportOverview
                    {
                        Name = fixture.Sport
                    };

                    sports.Add(fixture.Sport, sportoverview);
                }
                else
                    sportoverview = sports[fixture.Sport];

                sportoverview.Total++;

                switch(fixture.ListenerOverview.MatchStatus)
                {
                    case MatchStatus.Ready: case MatchStatus.Setup:
                        sportoverview.InSetup++;
                        break;
                    case MatchStatus.Prematch:
                        sportoverview.InPreMatch++;
                        break;
                    default:
                        sportoverview.InPlay++;
                        break;
                }

                if (fixture.ListenerOverview.IsErrored.HasValue && fixture.ListenerOverview.IsErrored.Value)
                    sportoverview.InErrorState++;
            }

            return sports.Values;
        }

        public ISportDetails GetSportDetail(string sportCode)
        {
            if (string.IsNullOrEmpty(sportCode))
                return null;

            SportDetails details = new SportDetails { Name = sportCode };

            foreach(var fixture in Supervisor.GetFixtures())
            {
                if(string.Equals(fixture.Sport, sportCode))
                {
                    details.Total++;

                    switch (fixture.ListenerOverview.MatchStatus)
                    {
                        case MatchStatus.Ready:
                        case MatchStatus.Setup:
                            details.InSetup++;
                            break;
                        case MatchStatus.Prematch:
                            details.InPreMatch++;
                            break;
                        default:
                            details.InPlay++;
                            break;
                    }

                    if (fixture.ListenerOverview.IsErrored.HasValue && fixture.ListenerOverview.IsErrored.Value)
                        details.InErrorState++;

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
            // TODO
            return new AdapterStatus
            {
                AdapterVersion = "1.2",
                PluginName = "Testing",
                PluginVersion = "3.4",
                UdapiSDKVersion = "5.6",
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

        public IEnumerable<IFixtureOverview> GetFixtures()
        {
            return Supervisor.GetFixtures().Select(CreateFixtureOverview).ToList();
        }

        private static IFixtureOverview CreateFixtureOverview(Model.Interface.IFixtureOverview fixture)
        {
            FixtureOverview ret = new FixtureOverview();
            FillFixtureOverview(ret, fixture);
            return ret;
        }

        private static void FillFixtureOverview(FixtureOverview to, Model.Interface.IFixtureOverview from)
        {
            to.Id = from.Id;
            to.IsStreaming = from.ListenerOverview.IsStreaming.GetValueOrDefault();
            to.IsInErrorState = from.ListenerOverview.IsErrored.GetValueOrDefault();
            to.StartTime = from.ListenerOverview.StartTime.GetValueOrDefault();
            to.Competition = from.CompetitionName;
            to.CompetitionId = from.CompetitionId;
            to.Description = from.Name;
            to.Sequence = from.ListenerOverview.Sequence.GetValueOrDefault().ToString();
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
    }
}

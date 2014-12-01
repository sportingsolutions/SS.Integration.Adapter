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
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Diagnostics.Model.Service.Model;
using SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;

namespace SS.Integration.Adapter.Diagnostics.RestService
{
    /// <summary>
    /// Only useful for testing purposes
    /// </summary>
    public class MockedSupervisor : ISupervisorProxy
    {
        private static ISportOverview GenerateMockedSportOverview(string sportCode)
        {
            return new SportOverview
            {
                Name = sportCode,
                Total = 10,
                InSetup = 2,
                InPreMatch = 3,
                InPlay = 5,
                InErrorState = 3
            };
        }

        private static ISportDetails GenerateMockedSportDetail(string sportCode)
        {
            SportDetails detail = new SportDetails
            {
                Name = sportCode,
                Total = 5,
                InSetup = 2,
                InPreMatch = 1,
                InPlay = 2,
                InErrorState = 2
            };

            detail.AddFixture(new FixtureOverview { Id = "123", IsStreaming = true,  State = FixtureState.Running,  IsInErrorState = false,  Competition = "Premier League",    CompetitionId = "123212112", StartTime = new DateTime(2014, 2, 17, 9, 0, 0),  Description = "Chelsea v QPR", Sequence = "10"});
            detail.AddFixture(new FixtureOverview { Id = "234", IsStreaming = true,  State = FixtureState.PreMatch, IsInErrorState = true,   Competition = "Premier League",    CompetitionId = "fffffffff", StartTime = new DateTime(2014, 2, 17, 14, 0, 0), Description = "Manchester United v Arsenal", Sequence = "12" });
            detail.AddFixture(new FixtureOverview { Id = "345", IsStreaming = false, State = FixtureState.Over,     IsInErrorState = false,  Competition = "Champions League",  CompetitionId = "AAAAAAAAA", StartTime = new DateTime(2014, 3, 18, 20, 0, 0), Description = "Tottenham v Juventus", Sequence = "84" });
            detail.AddFixture(new FixtureOverview { Id = "456", IsStreaming = false, State = FixtureState.Setup,    IsInErrorState = true,   Competition = "Serie A",           CompetitionId = "823702122", StartTime = new DateTime(2014, 2, 17, 9, 0, 0),  Description = "Milan v Inter", Sequence = "3" });
            detail.AddFixture(new FixtureOverview { Id = "567", IsStreaming = false, State = FixtureState.Ready,    IsInErrorState = false , Competition = "French Division 1", CompetitionId = "1qqqqqqas", StartTime = new DateTime(2014, 3, 17, 17, 0, 0), Description = "PSG v Lion", Sequence = "99" });

            return detail;
        }

        private static IFixtureDetails GenerateMockedFixtureOverview(string fixtureId)
        {
            var tmp = new FixtureDetails
            {
                Id = fixtureId,
                IsStreaming = true,
                State = FixtureState.Ready,
                Competition = "French Division 1",
                CompetitionId = "1qqqqqq",
                StartTime = new DateTime(2014, 3, 17, 17, 0, 0),
                Description = "PSG v Lion",
                Sequence = "5",
                IsOver = false,
                IsDeleted = false,
                //ConnectionState = FixtureDetails.ConnectionStatus.CONNECTED
            };

            tmp.AddProcessingEntry(new FixtureProcessingEntry { Sequence = "1", Epoch = "1", IsUpdate = false, State = FixtureProcessingState.PROCESSED, Timestamp = new DateTime(2013, 06, 11, 14, 33, 0) });
            tmp.AddProcessingEntry(new FixtureProcessingEntry { Sequence = "2", Epoch = "1", IsUpdate = true, State = FixtureProcessingState.PROCESSED, Timestamp = new DateTime(2013, 06, 11, 14, 34, 0), Exception = "Null pointer exception" });
            tmp.AddProcessingEntry(new FixtureProcessingEntry { Sequence = "2", Epoch = "1", IsUpdate = false, State = FixtureProcessingState.PROCESSED, Timestamp = new DateTime(2013, 06, 11, 14, 34, 30) });
            tmp.AddProcessingEntry(new FixtureProcessingEntry { Sequence = "3", Epoch = null, IsUpdate = true, State = FixtureProcessingState.SKIPPED, Timestamp = new DateTime(2013, 06, 11, 14, 35, 0) });
            tmp.AddProcessingEntry(new FixtureProcessingEntry { Sequence = "4", Epoch = null, IsUpdate = true, State = FixtureProcessingState.PROCESSED, Timestamp = new DateTime(2013, 06, 11, 14, 37, 0) });
            tmp.AddProcessingEntry(new FixtureProcessingEntry { Sequence = "5", Epoch = "2", IsUpdate = true, State = FixtureProcessingState.SKIPPED, Timestamp = new DateTime(2013, 06, 11, 14, 38, 45), EpochChangeReasons = new[] { 10 }, });
            tmp.AddProcessingEntry(new FixtureProcessingEntry { Sequence = "5", Epoch = "2", IsUpdate = false, State = FixtureProcessingState.PROCESSING, Timestamp = new DateTime(2013, 06, 11, 14, 39, 0) });

            return tmp;
        }

        public IEnumerable<ISportOverview> GetSports()
        {
            List<ISportOverview> sports = new List<ISportOverview>();
            foreach (var sport in new[] { "Football", "RugbyUnion", "RugbyLeague", "Darts", "Cricket", "TestCricket", "AmericanFootball", "Basketball", "Baseball", "HorseRacing" })
            {
                sports.Add(GenerateMockedSportOverview(sport));
            }

            return sports;
        }

        public ISportDetails GetSportDetail(string sportCode)
        {
            return GenerateMockedSportDetail(sportCode);
        }

        public IFixtureDetails GetFixtureDetail(string fixtureId)
        {
            return GenerateMockedFixtureOverview(fixtureId);
        }

        public IAdapterStatus GetAdapterStatus()
        {
            return new AdapterStatus
            {
                AdapterVersion = "1.2.3",
                PluginName = "Testing",
                PluginVersion = "4.5.6",
                UdapiSDKVersion = "7.8.9",
                RunningThreads = "12",
                MemoryUsage = "150000"
            };            
        }


        public IEnumerable<IFixtureProcessingEntry> GetFixtureHistory(string fixtureId)
        {
            // TODO
            return new List<IFixtureProcessingEntry>();
        }


        public IEnumerable<IFixtureOverview> GetFixtures()
        {
            var sport = GenerateMockedSportDetail("");
            return sport.Fixtures;
        }

        public void Dispose()
        {
        }
    }
}

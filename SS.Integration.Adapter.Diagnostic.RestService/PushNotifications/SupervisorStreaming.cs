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

using Microsoft.AspNet.SignalR;

namespace SS.Integration.Adapter.Diagnostic.RestService.PushNotifications
{
    public class SupervisorStreaming : Hub
    {
        private const string SPORT_GROUP_PREFIX = "SportGroup-";
        private const string FIXTURE_GROUP_PREFIX = "FixtureGroup-";

        public void JoinSportGroup(string sport)
        {
            if(string.IsNullOrEmpty(sport))
                return;

            Groups.Add(Context.ConnectionId, SPORT_GROUP_PREFIX + sport);
        }

        public void LeaveSportGroup(string sport)
        {
            if (string.IsNullOrEmpty(sport))
                return;

            Groups.Remove(Context.ConnectionId, SPORT_GROUP_PREFIX + sport);
        }

        public void JoinFixtureGroup(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return;

            Groups.Add(Context.ConnectionId, FIXTURE_GROUP_PREFIX + fixtureId);
        }

        public void LeaveFixtureGroup(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return;

            Groups.Remove(Context.ConnectionId, FIXTURE_GROUP_PREFIX + fixtureId);
        }

        public void OnSportUpdate(string sport, object update)
        {
            if (update == null || string.IsNullOrEmpty(sport))
                return;

            var group = Clients.Group(SPORT_GROUP_PREFIX + sport);
            if (group != null)
                group.updateSport(update);
        }

        public void OnFixtureUpdate(string fixtureId, object update)
        {
            if (update == null || string.IsNullOrEmpty(fixtureId))
                return;

            var group = Clients.Group(FIXTURE_GROUP_PREFIX + fixtureId);
            if (group != null)
                group.updateFixture(update);
        }
    }
}
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

namespace SS.Integration.Adapter.Diagnostics.RestService.PushNotifications
{
    public class SupervisorStreamingHub : Hub
    {
    

        public void JoinSportGroup(string sport)
        {
            if(string.IsNullOrEmpty(sport))
                return;

            Groups.Add(Context.ConnectionId, SupervisorStreamingConstants.SPORT_GROUP_PREFIX + sport);
        }

        public void LeaveSportGroup(string sport)
        {
            if (string.IsNullOrEmpty(sport))
                return;

            Groups.Remove(Context.ConnectionId, SupervisorStreamingConstants.SPORT_GROUP_PREFIX + sport);
        }

        public void JoinFixtureGroup(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return;

            Groups.Add(Context.ConnectionId, SupervisorStreamingConstants.FIXTURE_GROUP_PREFIX + fixtureId);
        }

        public void LeaveFixtureGroup(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return;

            Groups.Remove(Context.ConnectionId, SupervisorStreamingConstants.FIXTURE_GROUP_PREFIX + fixtureId);
        }

        public void JoinAdapterGroup()
        {
            Groups.Add(Context.ConnectionId, SupervisorStreamingConstants.ADAPTER_GROUP);
        }

        public void LeaveAdapterGroup()
        {
            Groups.Remove(Context.ConnectionId, SupervisorStreamingConstants.ADAPTER_GROUP);
        }
    }
}
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
using log4net;
using Microsoft.AspNet.SignalR;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;

namespace SS.Integration.Adapter.Diagnostics.RestService.PushNotifications
{
    public class SupervisorStreamingService : ISupervisorStreamingService
    {
        private static SupervisorStreamingService _instance = new SupervisorStreamingService();
        private readonly IHubContext _hub;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(SupervisorStreamingService));

        private SupervisorStreamingService()
        {
            _hub = GlobalHost.ConnectionManager.GetHubContext<SupervisorStreamingHub>();
        }

        public static SupervisorStreamingService Instance => _instance ?? (_instance = new SupervisorStreamingService());


        public void OnSportUpdate(ISportDetails sport)
        {
            if (sport == null || string.IsNullOrEmpty(sport.Name))
                return;

            try
            {
                var group = _hub.Clients.Group(SupervisorStreamingConstants.SPORT_GROUP_PREFIX + sport.Name);
                if (group != null)
                    group.OnSportUpdate(sport);
            }
            catch(Exception e)
            {
                _logger.Error(string.Format("Error while sending update on sport={0}", sport.Name), e);
            }
        }

        public void OnFixtureUpdate(IFixtureDetails fixture)
        {
            if (fixture == null || string.IsNullOrEmpty(fixture.Id))
                return;

            try
            {
                var group = _hub.Clients.Group(SupervisorStreamingConstants.FIXTURE_GROUP_PREFIX + fixture.Id);
                if (group != null)
                    group.OnFixtureUpdate(fixture);
            }
            catch(Exception e)
            {
                _logger.Error(string.Format("Error while sending update for fixtureId={0}", fixture.Id), e);
            }
        }

        public void OnAdapterUpdate(IAdapterStatus update)
        {
            if (update == null)
                return;

            try
            {
                var group = _hub.Clients.Group(SupervisorStreamingConstants.ADAPTER_GROUP);
                if (group != null)
                    group.OnAdapterUpdate(update);
            }
            catch(Exception e)
            {
                _logger.Error("Error while sending update for adapter", e);
            }
        }

        public void OnError(IProcessingEntryError update)
        {
            if (update == null)
                return;

            try
            {
                var group = _hub.Clients.Group(SupervisorStreamingConstants.ADAPTER_GROUP);
                if (group != null)
                    group.OnError(update);
            }
            catch (Exception e)
            {
                _logger.Error("Error while sending error update for adapter", e);
            }
        }
    }
}
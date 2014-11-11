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

using System.Threading.Tasks;
using log4net;
using Microsoft.AspNet.SignalR;

namespace SS.Integration.Adapter.Diagnostics.RestService.PushNotifications
{
    public class SupervisorFacade
    {
        private static SupervisorFacade _instance = new SupervisorFacade();
        private readonly IHubContext _hub;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(SupervisorFacade));

        private SupervisorFacade()
        {
            _hub = GlobalHost.ConnectionManager.GetHubContext<SupervisorStreaming>();
        }

        public static SupervisorFacade Instance
        {
            get { return _instance ?? (_instance = new SupervisorFacade()); }
        }


        public void OnSportUpdate(string sport, object update)
        {
            if (update == null || string.IsNullOrEmpty(sport))
                return;

            Task.Factory.StartNew(() =>
            {
                var group = _hub.Clients.Group(SupervisorConstants.SPORT_GROUP_PREFIX + sport);
                if (group != null)
                    group.OnSportUpdate(update);

            }).ContinueWith(t =>
            {
                if (t.IsFaulted || t.Status == TaskStatus.Faulted)
                {
                    _logger.Error(string.Format("Error while sending update on sport={0}", sport), t.Exception);
                }
            });
        }

        public void OnFixtureUpdate(string fixtureId, object update)
        {
            if (update == null || string.IsNullOrEmpty(fixtureId))
                return;

            Task.Factory.StartNew(() =>
            {
                var group = _hub.Clients.Group(SupervisorConstants.FIXTURE_GROUP_PREFIX + fixtureId);
                if (group != null)
                    group.OnFixtureUpdate(update);

            }).ContinueWith(t =>
            {
                if (t.IsFaulted || t.Status == TaskStatus.Faulted)
                {
                    _logger.Error(string.Format("Error while sending update for fixtureId={0}", fixtureId), t.Exception);
                }
            });
        }

        public void OnAdapterUpdate(object update)
        {
            if (update == null)
                return;

            Task.Factory.StartNew(() =>
            {
                var group = _hub.Clients.Group(SupervisorConstants.ADAPTER_GROUP);
                if (group != null)
                    group.OnAdapterUpdate(update);

            }).ContinueWith(t =>
            {
                if (t.IsFaulted || t.Status == TaskStatus.Faulted)
                {
                    _logger.Error("Error while sending update for adapter", t.Exception);
                }
            });
        }

        public void OnError(object update)
        {
            if (update == null)
                return;

            Task.Factory.StartNew(() =>
            {
                var group = _hub.Clients.Group(SupervisorConstants.ADAPTER_GROUP);
                if (group != null)
                    group.OnError(update);

            }).ContinueWith(t =>
            {
                if (t.IsFaulted || t.Status == TaskStatus.Faulted)
                {
                    _logger.Error("Error while sending error update for adapter", t.Exception);
                }
            });
        }
    }
}
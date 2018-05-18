//Copyright 2017 Spin Services Limited

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
using System.Linq;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class has the responibility to process all the sports in parallel using different instances/threads managed by AKKA Router 
    /// A single sport is processed on a separate thread by a single instance acting like a child actor.
    /// The child actor is responsible for triggering the creation/processing of that sport's resources stream listeners to the StreamListenerManagerActor
    /// </summary>
    public class SportProcessorRouterActor : ReceiveActor
    {
        #region Constants

        public const string ActorName = nameof(SportProcessorRouterActor);
        public const string Path = "/user/" + ActorName;

        #endregion

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(SportProcessorRouterActor));
        private readonly IServiceFacade _serviceFacade;

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceFacade"></param>
        public SportProcessorRouterActor(IServiceFacade serviceFacade)
        {
            _serviceFacade = serviceFacade ?? throw new ArgumentNullException(nameof(serviceFacade));

            Receive<ProcessSportMsg>(o => ProcessSportMsgHandler(o));
            
        }

        #endregion

        #region Protected methods

        protected override void PreRestart(Exception reason, object message)
        {
            _logger.Error(
                $"Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));
            base.PreRestart(reason, message);
        }

        #endregion

        #region Private methods

        private void ProcessSportMsgHandler(ProcessSportMsg msg)
        {
            var sports = _serviceFacade.GetSports();
            List<IResourceFacade> resources = new List<IResourceFacade>();
            var s = "";
            foreach (var sport in sports)
            {

                var _res = _serviceFacade.GetResources(sport.Name);
                
                if (ValidateResources(_res, sport.Name))
                {
                    resources.AddRange(_res);
                }
                s += $"sport={sport.Name} count={_res.Count} all={resources.Count}{Environment.NewLine}";
            }

            _logger.Debug($"ProcessSportMsgHandler result {s}");
            List<IResourceFacade> resources40 = new List<IResourceFacade>();
            List<IResourceFacade> resources30 = new List<IResourceFacade>();
            if (resources.Count > 1)
            {
               /*
                foreach (var it in resources)
                {
                    if (it.MatchStatus == MatchStatus.InRunning)
                    {
                        resources40.Add(it);
                        resources.Remove(it);
                    }
                    else
                    if (it.MatchStatus == MatchStatus.Prematch)
                    {
                        resources30.Add(it);
                        resources.Remove(it);
                    }
                }
                */
                 resources.Sort((x, y) =>
                {
                    if (x.Content.MatchStatus == y.Content.MatchStatus)
                        return 0;

                    if (x.Content.MatchStatus == 40)
                        return -1;

                    if (y.Content.MatchStatus == 40)
                        return 1;

                    if (x.Content.MatchStatus == 30)
                        return -1;

                    if (y.Content.MatchStatus == 30)
                        return 1;

                    if (x.Content.MatchStatus < y.Content.MatchStatus)
                        return -1;

                    if (x.Content.MatchStatus > y.Content.MatchStatus)
                        return 1;

                    return 0;
                });
            }

            s = "";
            foreach (var resource in resources)
            {
                s += $"{resource.MatchStatus}  ";
            }

            _logger.Debug($"ProcessSportMsgHandler sort: {s}");

            var streamListenerManagerActor = Context.System.ActorSelection(StreamListenerManagerActor.Path);
            /*
            foreach (var resource in resources40)
            {
                streamListenerManagerActor.Tell(new ProcessResourceMsg { Resource = resource }, Self);
            }

            foreach (var resource in resources30)
            {
                streamListenerManagerActor.Tell(new ProcessResourceMsg { Resource = resource }, Self);
            }
            */
            foreach (var resource in resources)
            {
                streamListenerManagerActor.Tell(new ProcessResourceMsg { Resource = resource }, Self);
            }

        }

        /*
        private void ProcessSportMsgHandler(ProcessSportMsg msg)
        {
            var resources = _serviceFacade.GetResources(msg.Sport);
            if (ValidateResources(resources, msg.Sport))
            {
                _logger.Debug($"Received {resources.Count} fixtures to process in sport={msg.Sport}");

                if (resources.Count > 1)
                {
                    resources.Sort((x, y) =>
                    {
                        if (x.Content.MatchStatus > y.Content.MatchStatus)
                            return -1;

                        return x.Content.MatchStatus < y.Content.MatchStatus
                            ? 1
                            : DateTime.Parse(x.Content.StartTime).CompareTo(DateTime.Parse(y.Content.StartTime));

                    });
                }

                var streamListenerManagerActor = Context.System.ActorSelection(StreamListenerManagerActor.Path);

                foreach (var resource in resources)
                {
                    streamListenerManagerActor.Tell(new ProcessResourceMsg { Resource = resource }, Self);
                }
            }
        }
        */

        private bool ValidateResources(IList<IResourceFacade> resources, string sport)
        {
            var valid = true;

            if (resources == null)
            {
                _logger.Warn($"Cannot find sport={sport} in UDAPI....");
                valid = false;
            }
            else if (resources.Count == 0)
            {
                valid = false;
            }

            return valid;
        }

        #endregion
    }
}

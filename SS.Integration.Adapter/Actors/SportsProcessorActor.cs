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
using System.Linq;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class has the responibility to repeatedly schedule all sports processing at specified interval (default 60 seconds)
    /// Also Statistics Generation is triggered with each interval
    /// </summary>
    public class SportsProcessorActor : ReceiveActor
    {
        #region Constants

        public const string ActorName = nameof(SportsProcessorActor);
        public const string Path = "/user/" + ActorName;

        #endregion

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(SportsProcessorActor).ToString());
        private readonly IServiceFacade _serviceFacade;
        private readonly IActorRef _sportProcessorRouterActor;
        private readonly ICancelable _processSportsMsgSchedule;

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="serviceFacade"></param>
        /// <param name="sportProcessorRouterActor"></param>
        public SportsProcessorActor(
            ISettings settings,
            IServiceFacade serviceFacade,
            IActorRef sportProcessorRouterActor)
        {
            _serviceFacade = serviceFacade ?? throw new ArgumentNullException(nameof(serviceFacade));
            _sportProcessorRouterActor = sportProcessorRouterActor ?? throw new ArgumentNullException(nameof(sportProcessorRouterActor));

            Receive<ProcessSportsMsg>(o => ProcessSportsMsgHandler());

            _processSportsMsgSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(settings.FixtureCheckerFrequency),
                Self,
                new ProcessSportsMsg(),
                Self);
        }

        #endregion

        #region Message Handlers

        private void ProcessSportsMsgHandler()
        {
            var sports = _serviceFacade.GetSports();

            foreach (var sport in sports.Where(_ => _.Name == "Football"))
            {
                _sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = sport.Name });
            }
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
            _processSportsMsgSchedule.Cancel();
            base.PreRestart(reason, message);
        }

        #endregion
    }
}
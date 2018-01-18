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
using System.Collections.ObjectModel;
using System.Linq;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Enums;
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

        #region Attribues

        private readonly ILog _logger = LogManager.GetLogger(typeof(SportsProcessorActor).ToString());
        private readonly IServiceFacade _serviceFacade;
        private readonly IActorRef _sportProcessorRouterActor;
        private readonly ICancelable _processSportsMsgSchedule;

        private static readonly Dictionary<string, FixtureStats> FixtureStatsPerSport =
            new Dictionary<string, FixtureStats>();

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
            Receive<NewStreamListenerActorMsg>(a => NewStreamListenerActorMsgHandler(a));
            Receive<StreamListenerActorStateChangedMsg>(a => StreamListenerActorStateChangedMsgHandler(a));

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
            LogPublishedFixturesCounts();

            var sports = _serviceFacade.GetSports();

            foreach (var sport in sports)
            {
                _sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = sport.Name });
            }
        }

        private void LogPublishedFixturesCounts()
        {
            var publishedFixturesTotalCount = FixtureStatsPerSport.Count > 0
                ? FixtureStatsPerSport.Keys.SelectMany(sport =>
                        FixtureStatsPerSport[sport].FixtureStateCount.Keys.Select(state =>
                            FixtureStatsPerSport[sport].FixtureStateCount[state]))
                    .Sum()
                : 0;

            _logger.Info($"PublishedFixturesTotalCount={publishedFixturesTotalCount}");

            var streamListenerStates = Enum.GetValues(typeof(StreamListenerState)).Cast<StreamListenerState>();
            foreach (var state in streamListenerStates)
            {
                var publishedFixturesPerStateTotalCount = FixtureStatsPerSport.Count > 0
                    ? FixtureStatsPerSport.Keys
                        .Select(sport => FixtureStatsPerSport[sport].FixtureStateCount[state])
                        .Sum()
                    : 0;
                _logger.Info($"PublishedFixturesTotalCount={publishedFixturesPerStateTotalCount} having StreamListenerState={state}");
            }

            foreach (var sport in FixtureStatsPerSport.Keys)
            foreach (var state in FixtureStatsPerSport[sport].FixtureStateCount.Keys)
                _logger.Info(
                    $"PublishedFixturesCount={FixtureStatsPerSport[sport].FixtureStateCount[state]} having StreamListenerState={state} for Sport={sport}");
        }

        private void NewStreamListenerActorMsgHandler(NewStreamListenerActorMsg msg)
        {
            if (!FixtureStatsPerSport.ContainsKey(msg.Sport))
            {
                FixtureStatsPerSport.Add(msg.Sport, new FixtureStats());
            }

            FixtureStatsPerSport[msg.Sport].IncrementInstancesCount(StreamListenerState.Initializing);
        }

        private void StreamListenerActorStateChangedMsgHandler(StreamListenerActorStateChangedMsg msg)
        {
            FixtureStatsPerSport[msg.Sport].DecrementInstancesCount(msg.PreviousState);
            FixtureStatsPerSport[msg.Sport].IncrementInstancesCount(msg.NewState);
        }

        #endregion

        #region Private methods

        protected override void PreRestart(Exception reason, object message)
        {
            _processSportsMsgSchedule.Cancel();
            base.PreRestart(reason, message);
        }

        #endregion

        #region Private types

        private class FixtureStats
        {
            private readonly Dictionary<StreamListenerState, int> _fixtureStateCountDic;

            public ReadOnlyDictionary<StreamListenerState, int> FixtureStateCount { get; }

            public FixtureStats()
            {
                FixtureStateCount = new ReadOnlyDictionary<StreamListenerState, int>(
                    _fixtureStateCountDic = new Dictionary<StreamListenerState, int>());
                foreach (var state in Enum.GetValues(typeof(StreamListenerState)).Cast<StreamListenerState>())
                {
                    _fixtureStateCountDic.Add(state, 0);
                }
            }

            public void IncrementInstancesCount(StreamListenerState state)
            {
                _fixtureStateCountDic[state]++;
            }

            public void DecrementInstancesCount(StreamListenerState state)
            {
                if (_fixtureStateCountDic[state] > 0)
                    _fixtureStateCountDic[state]--;
            }
        }

        #endregion
    }
}
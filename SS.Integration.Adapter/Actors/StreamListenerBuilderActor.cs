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
using SS.Integration.Adapter.Enums;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// StreamListener Builder is responsible for mananging concurrent creation of stream listeners
    /// </summary>
    public class StreamListenerBuilderActor : ReceiveActor, IWithUnboundedStash
    {
        #region Constants

        public const string ActorName = nameof(StreamListenerBuilderActor);
        public const int CheckStateIntervalInMilliseconds = 10000;

        #endregion

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerBuilderActor));
        private readonly ISettings _settings;
        private readonly IActorContext _streamListenerManagerActorContext;
        private readonly IAdapterPlugin _adapterPlugin;
        private readonly IStateManager _stateManager;
        private readonly ISuspensionManager _suspensionManager;
        private readonly IStreamHealthCheckValidation _streamHealthCheckValidation;
        private readonly IFixtureValidation _fixtureValidation;
        //this field ensures throttling concurrent creation of stream listener actor instances
        private int _concurrentInitializations;
        //this is used to save fixture id of already created fixtures that haven't responded back
        private readonly HashSet<string> _fixtureIdSet = new HashSet<string>();

        #endregion

        #region Properties

        internal StreamListenerBuilderState State { get; private set; }

        internal int ConcurrentInitializations => _concurrentInitializations;

        public IStash Stash { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="streamListenerManagerActorContext"></param>
        /// <param name="adapterPlugin"></param>
        /// <param name="stateManager"></param>
        /// <param name="suspensionManager"></param>
        /// <param name="streamHealthCheckValidation"></param>
        /// <param name="fixtureValidation"></param>
        public StreamListenerBuilderActor(
            ISettings settings,
            IActorContext streamListenerManagerActorContext,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager,
            ISuspensionManager suspensionManager,
            IStreamHealthCheckValidation streamHealthCheckValidation,
            IFixtureValidation fixtureValidation)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _streamListenerManagerActorContext =
                streamListenerManagerActorContext ??
                throw new ArgumentNullException(nameof(streamListenerManagerActorContext));
            _adapterPlugin = adapterPlugin ?? throw new ArgumentNullException(nameof(adapterPlugin));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _suspensionManager = suspensionManager ?? throw new ArgumentNullException(nameof(suspensionManager));
            _streamHealthCheckValidation = streamHealthCheckValidation ?? throw new ArgumentNullException(nameof(streamHealthCheckValidation));
            _fixtureValidation = fixtureValidation ?? throw new ArgumentNullException(nameof(fixtureValidation));

            Context.System.Scheduler.ScheduleTellRepeatedly(
                CheckStateIntervalInMilliseconds,
                CheckStateIntervalInMilliseconds,
                Self,
                new CheckStreamListenerBuilderActorStateMsg(),
                Self);

            Active();
        }

        #endregion

        #region Behaviors

        //In the active state StreamListeners can be created on demand
        private void Active()
        {
            State = StreamListenerBuilderState.Active;

            _logger.Warn(
                $"Moved to Active State" +
                $" - _concurrentInitializations={_concurrentInitializations}");

            Receive<CheckStreamListenerBuilderActorStateMsg>(o => CheckStreamListenerBuilderActorStateMsgHandler(o));
            Receive<CheckFixtureStateMsg>(o => CheckFixtureStateMsgHandler(o));
            Receive<CreateStreamListenerMsg>(o => CreateStreamListenerMsgHandler(o));
            Receive<BuildStreamListenerActorMsg>(o => BuildStreamListenerActorMsgHandler(o));
            Receive<StreamListenerCreationCompletedMsg>(o => StreamListenerCreationCompletedMsgHandler(o));
            Receive<StreamListenerCreationCancelledMsg>(o => StreamListenerCreationCancelledMsgHandler(o));
            Receive<StreamListenerCreationFailedMsg>(o => StreamListenerCreationFailedMsgHandler(o));
        }

        //In the busy state the maximum concurrency has been already used and creation needs to be postponed until later
        private void Busy()
        {
            State = StreamListenerBuilderState.Busy;

            _logger.Warn(
                $"Moved to Busy State" +
                $" - fixture creation concurency limit of {_settings.FixtureCreationConcurrency} has been reached" +
                $" - _concurrentInitializations={_concurrentInitializations}");

            Receive<CheckStreamListenerBuilderActorStateMsg>(o => CheckStreamListenerBuilderActorStateMsgHandler(o));
            Receive<CheckFixtureStateMsg>(o => { Stash.Stash(); });
            Receive<CreateStreamListenerMsg>(o => { Stash.Stash(); });
            Receive<BuildStreamListenerActorMsg>(o => BuildStreamListenerActorMsgHandler(o));
            Receive<StreamListenerCreationCompletedMsg>(o => StreamListenerCreationCompletedMsgHandler(o));
            Receive<StreamListenerCreationCancelledMsg>(o => StreamListenerCreationCancelledMsgHandler(o));
            Receive<StreamListenerCreationFailedMsg>(o => StreamListenerCreationFailedMsgHandler(o));
        }

        #endregion

        #region Message Handlers

        //this is used to ensure we don't get blocked in Busy state
        //so we process self scheduled message at predefined interval to check/update the actor state and flags
        private void CheckStreamListenerBuilderActorStateMsgHandler(CheckStreamListenerBuilderActorStateMsg msg)
        {
            List<string> fixtureIdList = _fixtureIdSet.ToList();
            foreach (var fixtureId in fixtureIdList)
            {
                var streamListenerActorName = StreamListenerActor.GetName(fixtureId);
                var streamListenerActorRef = _streamListenerManagerActorContext.Child(streamListenerActorName);
                if (streamListenerActorRef.IsNobody())
                {
                    _logger.Warn(
                        $"CheckStreamListenerBuilderActorStateMsgHandler" +
                        $" - Stream Listener Instance for fixture with fixtureId={fixtureId} doesn't exist." +
                        $" - Going to remove it from the internal state.");
                    RemoveFixtureFromSet(fixtureId);
                }
                else
                {
                    StreamListenerState streamListenerActorState;
                    try
                    {
                        streamListenerActorState = streamListenerActorRef
                            .Ask<StreamListenerState>(
                                new GetStreamListenerActorStateMsg(),
                                TimeSpan.FromSeconds(10))
                            .Result;
                    }
                    catch (Exception)
                    {
                        //if we haven't heard back from StreamListenerActor in 10s we assume it's in Errored State
                        streamListenerActorState = StreamListenerState.Errored;
                    }
                    if (streamListenerActorState != StreamListenerState.Initializing)
                    {
                        _logger.Warn(
                            $"CheckStreamListenerBuilderActorStateMsgHandler" +
                            $" - Stream Listener Instance for fixture with fixtureId={fixtureId} has already been created and has state={streamListenerActorState}." +
                            $" - Going to remove it from the internal state.");
                        RemoveFixtureFromSet(fixtureId);
                    }
                    else
                    {
                        _logger.Warn(
                            $"CheckStreamListenerBuilderActorStateMsgHandler" +
                            $" - Stream Listener Instance for fixture with fixtureId={fixtureId} has already been created and is still in {streamListenerActorState} state");
                    }
                }
            }

            CheckStateUpdate();

            _logger.Debug(
                $"CheckStreamListenerBuilderActorStateMsgHandler completed" +
                $" - _concurrentInitializations={_concurrentInitializations}" +
                $" - _fixtureIdSet has {_fixtureIdSet.Count} items");
        }

        private void CreateStreamListenerMsgHandler(CreateStreamListenerMsg msg)
        {
            if (_concurrentInitializations + 1 > _settings.FixtureCreationConcurrency)
            {
                _logger.Warn(
                    $"CreateStreamListenerMsgHandler - {msg.Resource}"+
                    $" - fixture creation concurency limit of {_settings.FixtureCreationConcurrency} has been reached" +
                    $" - _concurrentInitializations={_concurrentInitializations}" +
                    $" - Moving to Busy State");
                Become(Busy);
                Self.Tell(msg);
            }
            else
            {
                if (msg.Resource.MatchStatus != MatchStatus.MatchOver)//match is not over so stream listener instance will be created
                {
                    _logger.Debug(
                        $"CreateStreamListenerMsgHandler - {msg.Resource} with MatchStatus={msg.Resource.MatchStatus}" +
                        $" - _concurrentInitializations={_concurrentInitializations}" +
                        $" - Going to create the Stream Listener Actor Instance");
                    SendBuildStreamListenerActorSelfMessage(msg.Resource);
                }
                else//if match is already over then check fixture state in order to validate stream listener instance creation
                {
                    _logger.Debug($"MatchOver detected for {msg.Resource} ; checking saved fixture state");
                    var fixtureStateActor = Context.System.ActorSelection(FixtureStateActor.Path);
                    fixtureStateActor.Tell(new CheckFixtureStateMsg { Resource = msg.Resource });
                }
            }
        }

        private void CheckFixtureStateMsgHandler(CheckFixtureStateMsg msg)
        {
            if (msg.ShouldProcessFixture)
            {
                _logger.Debug($"create StreamListenerActor instance for {msg.Resource} as saved fixture state is not MatchOver");
                SendBuildStreamListenerActorSelfMessage(msg.Resource);
            }
            else
            {
                _logger.Debug($"skip creating StreamListenerActor Instance as there is no saved fixture state for {msg.Resource}");
            }
        }

        private void BuildStreamListenerActorMsgHandler(BuildStreamListenerActorMsg msg)
        {
            var streamListenerActorName = StreamListenerActor.GetName(msg.Resource.Id);
            if (_streamListenerManagerActorContext.Child(streamListenerActorName).IsNobody())
            {
                _logger.Debug($"Building Stream Listener Instance for {msg.Resource}");

                _streamListenerManagerActorContext.ActorOf(Props.Create(() =>
                        new StreamListenerActor(
                            _settings,
                            _adapterPlugin,
                            msg.Resource,
                            _stateManager,
                            _suspensionManager,
                            _streamHealthCheckValidation,
                            _fixtureValidation)),
                    streamListenerActorName);

                if (!_fixtureIdSet.Contains(msg.Resource.Id))
                    _fixtureIdSet.Add(msg.Resource.Id);
            }
            else
            {
                _logger.Warn($"Stream Listener Instance for {msg.Resource} not created as existing instance has been found");
            }
        }

        private void StreamListenerCreationCompletedMsgHandler(StreamListenerCreationCompletedMsg msg)
        {
            RemoveFixtureFromSet(msg.FixtureId);
            CheckStateUpdate();

            _logger.Debug(
                $"StreamListenerCreationCompletedMsgHandler" +
                $" - fixtureId={msg.FixtureId}" +
                $" - FixtureStatus={msg.FixtureStatus}" +
                $" - _concurrentInitializations={_concurrentInitializations}" +
                $" - _fixtureIdSet has {_fixtureIdSet.Count} items");
        }

        private void StreamListenerCreationCancelledMsgHandler(StreamListenerCreationCancelledMsg msg)
        {
            RemoveFixtureFromSet(msg.FixtureId);
            CheckStateUpdate();

            _logger.Debug(
                $"StreamListenerCreationCancelledMsgHandler" +
                $" - fixtureId={msg.FixtureId}" +
                $" - FixtureStatus={msg.FixtureStatus}" +
                $" - StreamListenerCreationCancellationReason={msg.Reason}" +
                $" - _concurrentInitializations={_concurrentInitializations}" +
                $" - _fixtureIdSet has {_fixtureIdSet.Count} items");
        }

        private void StreamListenerCreationFailedMsgHandler(StreamListenerCreationFailedMsg msg)
        {
            RemoveFixtureFromSet(msg.FixtureId);
            CheckStateUpdate();

            _logger.Debug(
                $"StreamListenerCreationFailedMsgHandler" +
                $" - fixtureId={msg.FixtureId}" +
                $" - FixtureStatus={msg.FixtureStatus}"+
                $" - _concurrentInitializations={_concurrentInitializations}" +
                $" - _fixtureIdSet has {_fixtureIdSet.Count} items");
        }

        #endregion

        #region Private methods

        private void SendBuildStreamListenerActorSelfMessage(IResourceFacade resource)
        {
            _concurrentInitializations++;

            _logger.Debug(
                $"SendBuildStreamListenerActorSelfMessage for {resource}" +
                $" - _concurrentInitializations={_concurrentInitializations}");

            Self.Tell(new BuildStreamListenerActorMsg { Resource = resource });
        }

        private void RemoveFixtureFromSet(string fixtureId)
        {
            if (_fixtureIdSet.Contains(fixtureId))
                _fixtureIdSet.Remove(fixtureId);
            if (_concurrentInitializations > 0)
                _concurrentInitializations--;
        }

        private void CheckStateUpdate()
        {
            if (State != StreamListenerBuilderState.Active &&
                _concurrentInitializations < _settings.FixtureCreationConcurrency)
            {
                Become(Active);
            }

            Stash.Unstash();
        }

        #endregion

        #region Private messages

        private class BuildStreamListenerActorMsg
        {
            public IResourceFacade Resource { get; set; }
        }

        //this is used to ensure we don't get blocked in Busy state
        //so we process self scheduled message at predefined interval to check/update the actor state and flags
        private class CheckStreamListenerBuilderActorStateMsg
        {   
        }

        #endregion
    }
}

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
        //this is used to save fixture id of already created fixtures that haven't responded back
        private readonly HashSet<string> _creationInProgressFixtureIdSet = new HashSet<string>();

        #endregion

        #region Properties

        internal StreamListenerBuilderState State { get; private set; }

        internal int CreationInProgressFixtureIdSetCount => _creationInProgressFixtureIdSet.Count;

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

            _logger.Info(
                $"Moved to Active State" +
                $" - _creationInProgressFixtureIdSetCount={_creationInProgressFixtureIdSet.Count} items");

            Receive<CheckStreamListenerBuilderActorStateMsg>(o => CheckStreamListenerBuilderActorStateMsgHandler(o));
            Receive<CheckFixtureStateMsg>(o => CheckFixtureStateMsgHandler(o));
            Receive<CreateStreamListenerMsg>(o => CreateStreamListenerMsgHandler(o));
            Receive<StreamListenerCreationCompletedMsg>(o => StreamListenerCreationCompletedMsgHandler(o));
            Receive<StreamListenerCreationCancelledMsg>(o => StreamListenerCreationCancelledMsgHandler(o));
            Receive<StreamListenerCreationFailedMsg>(o => StreamListenerCreationFailedMsgHandler(o));

            Stash?.UnstashAll();
        }

        //In the busy state the maximum concurrency has been already used and creation needs to be postponed until later
        private void Busy()
        {
            State = StreamListenerBuilderState.Busy;

            _logger.Warn(
                $"Moved to Busy State" +
                $" - _creationInProgressFixtureIdSetCount={_creationInProgressFixtureIdSet.Count} items");

            Receive<CheckStreamListenerBuilderActorStateMsg>(o => CheckStreamListenerBuilderActorStateMsgHandler(o));
            Receive<CheckFixtureStateMsg>(o => { Stash.Stash(); });
            Receive<CreateStreamListenerMsg>(o => { Stash.Stash(); });
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
            List<string> fixtureIdList = _creationInProgressFixtureIdSet.ToList();
            foreach (var fixtureId in fixtureIdList)
            {
                var streamListenerActorName = StreamListenerActor.GetName(fixtureId);
                var streamListenerActorRef = _streamListenerManagerActorContext.Child(streamListenerActorName);
                if (streamListenerActorRef.IsNobody())
                {
                    _logger.Debug(
                        $"CheckStreamListenerBuilderActorStateMsgHandler" +
                        $" - fixtureId={fixtureId}" +
                        $" - StreamListenerActor instance doesn't exist. Going to remove it from the internal state.");
                    RemoveFixtureFromSet(fixtureId);
                }
                else
                {
                    StreamListenerState? streamListenerActorState;
                    try
                    {
                        streamListenerActorState = streamListenerActorRef
                            .Ask<StreamListenerState>(
                                new GetStreamListenerActorStateMsg(),
                                TimeSpan.FromSeconds(30))
                            .Result;
                    }
                    catch (Exception)
                    {
                        //if we haven't heard back from StreamListenerActor then we can't identify it's state
                        streamListenerActorState = null;
                    }

                    if (streamListenerActorState.HasValue &&
                        streamListenerActorState.Value != StreamListenerState.Initializing)
                    {
                        RemoveFixtureFromSet(fixtureId);
                    }

                    _logger.Debug(
                        $"CheckStreamListenerBuilderActorStateMsgHandler" +
                        $" - fixtureId={fixtureId}" +
                        $" - streamListenerActorState={streamListenerActorState?.ToString() ?? "null"}" +
                        $" - StreamListenerActor instance has already been created");
                }
            }

            CheckActiveState();

            _logger.Debug(
                $"CheckStreamListenerBuilderActorStateMsgHandler completed" +
                $" - _creationInProgressFixtureIdSetCount={_creationInProgressFixtureIdSet.Count} items");
        }

        private void CreateStreamListenerMsgHandler(CreateStreamListenerMsg msg)
        {
            _logger.Info(
                $"CreateStreamListenerMsgHandler - {msg.Resource}" +
                $" - _creationInProgressFixtureIdSetCount={_creationInProgressFixtureIdSet.Count} items");

            if (msg.Resource.MatchStatus != MatchStatus.MatchOver)
            {
                BuildStreamListenerActorInstance(msg, msg.Resource);
            }
            else //if match is already over then check fixture state in order to validate stream listener instance creation
            {
                _logger.Debug(
                    $"CreateStreamListenerMsgHandler - {msg.Resource} has MatchStatus=MatchOver" +
                    $" - checking saved fixture state");
                var fixtureStateActor = Context.System.ActorSelection(FixtureStateActor.Path);
                fixtureStateActor.Tell(new CheckFixtureStateMsg { Resource = msg.Resource });
            }
        }

        private void CheckFixtureStateMsgHandler(CheckFixtureStateMsg msg)
        {
            if (msg.ShouldProcessFixture)
            {
                _logger.Debug(
                    $"CheckFixtureStateMsgHandler - {msg.Resource}" +
                    $" - _creationInProgressFixtureIdSetCount={_creationInProgressFixtureIdSet.Count} items");

                BuildStreamListenerActorInstance(msg, msg.Resource);
            }
            else
            {
                _logger.Debug(
                    $"CheckFixtureStateMsgHandler - {msg.Resource}" +
                    $"skip creating StreamListenerActor instance as MatchStatus=MatchOver");
            }
        }

        private void StreamListenerCreationCompletedMsgHandler(StreamListenerCreationCompletedMsg msg)
        {
            RemoveFixtureFromSet(msg.FixtureId);
            CheckActiveState();

            _logger.Debug(
                $"Stream Listener creation Completed" +
                $" - fixtureId={msg.FixtureId}" +
                $" - FixtureStatus={msg.FixtureStatus}" +
                $" - _creationInProgressFixtureIdSetCount={_creationInProgressFixtureIdSet.Count} items");
        }

        private void StreamListenerCreationCancelledMsgHandler(StreamListenerCreationCancelledMsg msg)
        {
            RemoveFixtureFromSet(msg.FixtureId);
            CheckActiveState();

            _logger.Debug(
                $"Stream Listener creation Cancelled" +
                $" - fixtureId={msg.FixtureId}" +
                $" - FixtureStatus={msg.FixtureStatus}" +
                $" - StreamListenerCreationCancellationReason={msg.Reason}" +
                $" - _creationInProgressFixtureIdSetCount={_creationInProgressFixtureIdSet.Count} items");
        }

        private void StreamListenerCreationFailedMsgHandler(StreamListenerCreationFailedMsg msg)
        {
            RemoveFixtureFromSet(msg.FixtureId);
            CheckActiveState();

            _logger.Debug(
                $"Stream Listener creation Failed" +
                $" - fixtureId={msg.FixtureId}" +
                $" - FixtureStatus={msg.FixtureStatus}" +
                $" - _creationInProgressFixtureIdSetCount={_creationInProgressFixtureIdSet.Count} items");
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

        private void BuildStreamListenerActorInstance(object msg, IResourceFacade resource)
        {
            var streamListenerActorName = StreamListenerActor.GetName(resource.Id);
            if (_streamListenerManagerActorContext.Child(streamListenerActorName).IsNobody())
            {
                if (_creationInProgressFixtureIdSet.Count - 4 > _settings.FixtureCreationConcurrency)
                {
                    _logger.Warn(
                        $"BuildStreamListenerActorInstance - {resource}" +
                        $" - {msg.GetType().Name}" +
                        $" - fixture creation concurrency limit of {_settings.FixtureCreationConcurrency} has been reached" +
                        $" - Moving to Busy State" +
                        $" - _creationInProgressFixtureIdSetCount={_creationInProgressFixtureIdSet.Count} items");
                    Become(Busy);
                    Self.Tell(msg);
                }
                else
                {
                    _logger.Debug(
                        $"BuildStreamListenerActorInstance - {resource} with MatchStatus={resource.MatchStatus}" +
                        $" - {msg.GetType().Name}" +
                        $" - _creationInProgressFixtureIdSetCount={_creationInProgressFixtureIdSet.Count} items" +
                        $" - Going to create the Stream Listener Actor Instance");

                    _streamListenerManagerActorContext.ActorOf(Props.Create(() =>
                            new StreamListenerActor(
                                _settings,
                                _adapterPlugin,
                                resource,
                                _stateManager,
                                _suspensionManager,
                                _streamHealthCheckValidation,
                                _fixtureValidation)),
                        streamListenerActorName);

                    if (!_creationInProgressFixtureIdSet.Contains(resource.Id))
                    {
                        _creationInProgressFixtureIdSet.Add(resource.Id);
                    }
                    else
                    {
                        _logger.Warn($"Attempt to add second StreamListenerActor for {resource}");
                    }
                }
            }
            else
            {
                _logger.Debug(
                    $"BuildStreamListenerActorInstance - {resource}" +
                    $" - {msg.GetType().Name}" +
                    $" - StreamListenerActor instance not created as existing instance has been found");
            }
        }

        private void RemoveFixtureFromSet(string fixtureId)
        {
            if (_creationInProgressFixtureIdSet.Contains(fixtureId))
                _creationInProgressFixtureIdSet.Remove(fixtureId);
        }

        private void CheckActiveState()
        {
            //if current state is not Active and we have room for more processings then move to Active State
            if (State != StreamListenerBuilderState.Active &&
                _creationInProgressFixtureIdSet.Count < _settings.FixtureCreationConcurrency)
            {
                Become(Active);
            }
        }

        #endregion

        #region Private messages

        //this is used to ensure we don't get blocked in Busy state
        //so we process self scheduled message at predefined interval to check/update the actor state and flags
        private class CheckStreamListenerBuilderActorStateMsg
        {
        }

        #endregion
    }
}

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
using System.Reflection;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.TestKit;
using Akka.TestKit.NUnit;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using Akka.Routing;
using FluentAssertions;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamListenerActorTests : BaseTestKit
    {
        #region Constants

        public const int ASSERT_WAIT_TIMEOUT = 5000 /*ms*/;
        public const int ASSERT_EXEC_INTERVAL = 200 /*ms*/;
        public const string STREAM_LISTENER_ACTOR_CATEGORY = nameof(StreamListenerActor);

        #endregion

        #region Attributes

        private Mock<ISettings> _settingsMock;
        private Mock<IAdapterPlugin> _pluginMock;
        private Mock<IServiceFacade> _serviceMock;
        private Mock<IEventState> _eventStateMock;
        private Mock<IStateManager> _stateManagerMock;
        private Mock<IStateProvider> _stateProvider;
        private Mock<ISuspensionManager> _suspensionManager;

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            _pluginMock = new Mock<IAdapterPlugin>();

            _settingsMock = new Mock<ISettings>();
            _settingsMock.SetupGet(a => a.StreamSafetyThreshold).Returns(3);
            _settingsMock.SetupGet(a => a.FixtureCreationConcurrency).Returns(3);
            _settingsMock.SetupGet(a => a.FixtureCheckerFrequency).Returns(10000);
            _settingsMock.SetupGet(a => a.EventStateFilePath).Returns(string.Empty);

            _serviceMock = new Mock<IServiceFacade>();

            _eventStateMock = new Mock<IEventState>();

            _stateManagerMock = new Mock<IStateManager>();

            AdapterActorSystem.Init(
                _settingsMock.Object,
                _serviceMock.Object,
                _pluginMock.Object,
                _stateManagerMock.Object,
                Sys,
                false);
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures we don't process the first snapshot and we unsuspend the fixture,
        /// when the saved state is not null and current resource sequence has not changed compared to the saved state sequence.
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationDoNotProcessSnapshotIfSavedStateIsNotNullAndSequenceHasNotChanged()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we process the first snapshot on initialization,
        /// also we start streaming when the current resource state is not Ready nor Setup (for this test status is InPlay).
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationStartStreamingAndProcessFirstSnapshotWhenMatchStatusNotReadyNorSetup()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 1, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we process the match over when status has changed from InPlay to MatchOver
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationMoveToFinishedStateWhenResourceHasMatchOverStatus()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_matchover_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 1, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Once);
                    _stateManagerMock.Verify(a =>
                            a.ClearState(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Stopped, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we move directly to finished state on initialization when the fixture has status match over on the saved state
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationMoveToFinishedStateWhenMatchOverWasAlreadyProcessed()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_matchover_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 1, MatchStatus = MatchStatus.MatchOver },
                out snapshot,
                out resourceFacadeMock);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Never);
                    _stateManagerMock.Verify(a =>
                            a.ClearState(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Stopped, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we process the new match status change on initialization when the stored match status is different than the current one
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationProcessFullSnapshotWhenCurrentMatchStatusIsDifferentThanStoredMatchStatus()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 1, MatchStatus = MatchStatus.Prematch },
                out snapshot,
                out resourceFacadeMock);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Never);
                    _stateManagerMock.Verify(a =>
                            a.ClearState(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we start streaming when the stored match status is different than the current one 
        /// (which is not Setup with Allow Streaming in Setup disabled)
        /// Also we ensure the Unsuspend Fixture is called given the Sequence has not changed (stored Sequence is the same as the current one)
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationAfterStartStreamingUnSuspendFixtureOnProcessSnapshotWhenSequenceHasNotChanged()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we start streaming when the match status is Ready.
        /// Also the first snapshot is processed due to sequence change
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationStartStreamingWhenMatchStatusReady()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_ready_snapshot_3,
                /*storedData*/new { Epoch = 3, Sequence = 1, MatchStatus = MatchStatus.Ready },
                out snapshot,
                out resourceFacadeMock);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we move to Initialized state when Fixture Status in Setup and Streaming in Setup is disabled
        /// Also First Snapshot is not processed and Fixture is Unsuspended as the current sequence is the same as the stored one.
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationMoveToInitializedStateWhenMatchStatusSetupWithNotAllowStreamingInSetupMode()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_setup_snapshot_2,
                /*storedData*/new { Epoch = 3, Sequence = 2, MatchStatus = MatchStatus.Setup },
                out snapshot,
                out resourceFacadeMock);
            _settingsMock.SetupGet(a => a.AllowFixtureStreamingInSetupMode).Returns(false);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Initialized, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we move to Streaming State after processing resource state update message.
        /// Also Snapshot processing is done twice 
        /// - first snapshot processing is done on initialization due to different sequence numbers between stored and current
        /// - second snapshot processing is done after we process the resource state update message due to match status changed
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnUpdateResourceStateProcessSnapshotWhenMovingFromInitializedStateToStreamingState()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_setup_snapshot_2,
                /*storedData*/new { Epoch = 3, Sequence = 1, MatchStatus = MatchStatus.Setup },
                out snapshot,
                out resourceFacadeMock);
            var resourceFacadeUpdateMock = new Mock<IResourceFacade>();
            resourceFacadeUpdateMock.Setup(o => o.Id).Returns(resourceFacadeMock.Object.Id);
            resourceFacadeUpdateMock.Setup(o => o.IsMatchOver).Returns(false);
            resourceFacadeUpdateMock.Setup(o => o.MatchStatus).Returns(MatchStatus.InRunning);
            resourceFacadeUpdateMock.Setup(o => o.Content).Returns(new Summary
            {
                Id = resourceFacadeMock.Object.Id,
                Sequence = 3,
                MatchStatus = (int)MatchStatus.InRunning
            });
            _settingsMock.SetupGet(a => a.AllowFixtureStreamingInSetupMode).Returns(false);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            actor.Tell(new ResourceStateUpdateMsg { Resource = resourceFacadeUpdateMock.Object });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Exactly(2));
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Exactly(2));
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that after receiving update message and the fixture delta snapshot sequence is not valid (lower than current sequence), 
        /// then we ignore the snapshot and we don't process it
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnUpdateMessageProcessSnapshotNotDoneWhenSequenceInvalidLowerThanCurrentSequence()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            var update = new Fixture
            {
                Id = resourceFacadeMock.Object.Id,
                //invalid sequence
                Sequence = resourceFacadeMock.Object.Content.Sequence - 1,
                MatchStatus = ((int)resourceFacadeMock.Object.MatchStatus).ToString()
            };
            StreamMessage message = new StreamMessage { Content = update };

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that after receiving update message and the fixture delta snapshot sequence is not valid (more than 1 greater than current sequence), 
        /// then we process the full snapshot again. First snapshot processing happens on initialization due to different current sequence than stored one.
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnUpdateMessageProcessSnapshotWhenSequenceInvalidMoreThan1GreaterThanCurrentSequence()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 1, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock,
                (mockObj, snapshotJson) =>
                {
                    mockObj.SetupSequence(o => o.GetSnapshot())
                        .Returns(snapshotJson)
                        .Returns(snapshotJson.Replace(@"""Sequence"": 2,", @"""Sequence"": 4,"));
                });
            var update = new Fixture
            {
                Id = resourceFacadeMock.Object.Id,
                //invalid sequence
                Sequence = 4,
                MatchStatus = ((int)resourceFacadeMock.Object.MatchStatus).ToString()
            };
            StreamMessage message = new StreamMessage { Content = update };

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            resourceFacadeMock.Object.Content.Sequence = update.Sequence;
            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Exactly(2));
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Exactly(2));
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that after receiving the update message and the fixture delta snapshot epoch is not valid,
        /// because match status has changed so epoch has changed as well, 
        /// then we suspend the fixture and we process the full snapshot with epoch change.
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnUpdateMessageProcessEpochChangeWhenMatchStatusChange()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_ready_snapshot_3,
                /*storedData*/new { Epoch = 3, Sequence = 2, MatchStatus = MatchStatus.Ready },
                out snapshot,
                out resourceFacadeMock);
            var update = new Fixture
            {
                Id = resourceFacadeMock.Object.Id,
                Sequence = resourceFacadeMock.Object.Content.Sequence,
                Epoch = snapshot.Epoch + 1,
                MatchStatus = ((int)MatchStatus.InRunning).ToString(),
                LastEpochChangeReason = new[] { (int)EpochChangeReason.MatchStatus }
            };
            StreamMessage message = new StreamMessage { Content = update };

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Exactly(2));
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that after receiving the update message and the fixture delta snapshot epoch is not valid,
        /// because is lower than the current epoch, 
        /// then we suspend the fixture and we process the full snapshot with epoch change.
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnUpdateMessageWhenInvalidEpochLowerThanCurrentThenSuspendAndProcessSnapshot()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            var update = new Fixture
            {
                Id = resourceFacadeMock.Object.Id,
                Sequence = resourceFacadeMock.Object.Content.Sequence,
                Epoch = snapshot.Epoch - 1,
                MatchStatus = ((int)MatchStatus.InRunning).ToString()
            };
            StreamMessage message = new StreamMessage { Content = update };

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that after receiving the update message with valid sequence and valid epoch then stream update is processed
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnUpdateMessageProcessStreamUpdate()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            var update = new Fixture
            {
                Id = resourceFacadeMock.Object.Id,
                Sequence = resourceFacadeMock.Object.Content.Sequence + 1,
                Epoch = snapshot.Epoch,
                MatchStatus = ((int)MatchStatus.InRunning).ToString()
            };
            StreamMessage message = new StreamMessage { Content = update };

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that after receiving the update message 
        /// and the fixture delta snapshot sequence is valid (greater with 1 than current sequence) 
        /// and fixture delta snapshot epoch is not valid (greater than current epoch because of fixture deletion) 
        /// then we suspend the fixture with reason "fixture deleted" 
        /// and send "ProcessFixtureDeletion" to connector plugin 
        /// and stop streaming
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnUpdateMessageProcessFixtureDeletionWithEpochChange()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_ready_snapshot_3,
                /*storedData*/new { Epoch = 3, Sequence = 2, MatchStatus = MatchStatus.Ready },
                out snapshot,
                out resourceFacadeMock);
            var update = new Fixture
            {
                Id = resourceFacadeMock.Object.Id,
                Sequence = resourceFacadeMock.Object.Content.Sequence,
                Epoch = snapshot.Epoch + 1,
                MatchStatus = ((int)MatchStatus.Deleted).ToString(),
                LastEpochChangeReason = new[] { (int)EpochChangeReason.Deleted }
            };
            StreamMessage message = new StreamMessage { Content = update };

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    resourceFacadeMock.Verify(a => a.StopStreaming(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessFixtureDeletion(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_DELETED),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Stopped, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that on update message when json parsing error occurs we move to Errored State
        /// and try to recover by processing full snapshot, then move back to Streaming State
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnUpdateMessageParsingErrorSetErroredStateAndRecover()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));
            actor.Tell(new StreamUpdateMsg { Data = "This is a JSON message that will throw error on parsing" });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that on update message when json parsing error occurs we move to Errored State
        /// and try to recover by processing full snapshot, but we fail to recover so we remain in Errored State
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnUpdateMessageParsingErrorSetErroredStateAndFailToRecover()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new {Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning},
                out snapshot,
                out resourceFacadeMock,
                (mockObj, snapshotJson) =>
                {
                    mockObj.SetupSequence(o => o.GetSnapshot())
                        .Throws<System.Net.WebException>();
                });

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            AwaitAssert(() =>
                {
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            actor.Tell(new StreamUpdateMsg { Data = "This is a JSON message that will throw error on parsing" });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Errored, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that on update message when json parsing error occurs we move to Errored State
        /// and try to recover by processing full snapshot, but we fail to recover so we remain in Errored State,
        /// but after we get a new update message, we successfully recover by processing full snapshot
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnUpdateMessageParsingErrorSetErroredStateAndFailToRecoverThenRecoverOnNextUpdateMessage()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock,
                (mockObj, snapshotJson) =>
                {
                    mockObj.SetupSequence(o => o.GetSnapshot())
                        .Throws<System.Net.WebException>()
                        .Returns(snapshotJson)
                        .Throws<System.Net.WebException>();
                });
            var update = new Fixture
            {
                Id = resourceFacadeMock.Object.Id,
                Sequence = resourceFacadeMock.Object.Content.Sequence + 1,
                Epoch = snapshot.Epoch,
                MatchStatus = ((int)MatchStatus.InRunning).ToString()
            };
            StreamMessage message = new StreamMessage { Content = update };

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceFacadeMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    _settingsMock.Object));

            AwaitAssert(() =>
                {
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            //wait 1 second before sending bad update message
            Task.Delay(1000).Wait();
            actor.Tell(new StreamUpdateMsg { Data = "This is a JSON message that will throw error on parsing" });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Errored, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            //
            //Act
            //
            //wait another 2 seconds before sending correct update message
            Task.Delay(2000).Wait();
            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Exactly(2));
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that on Initialization when error occurs we move to Errored State
        /// and try to recover by processing full snapshot, but we fail to recover so we go to Stopped state
        /// where we notify the parent actor of stopping to kill this child actor
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializingFailedSetErroredStateAndFailToRecover()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            _serviceMock.Setup(o => o.GetResources(It.Is<string>(s => s.Equals("Football"))))
                .Returns(new List<IResourceFacade> { resourceFacadeMock.Object });
            //this will force initialization to fail
            resourceFacadeMock.Reset();
            resourceFacadeMock.Setup(o => o.Id).Returns(snapshot.Id);

            //
            //Act
            //
            var sportProcessorRouterActor =
                ActorOfAsTestActorRef<SportProcessorRouterActor>(
                    Props.Create(() =>
                            new SportProcessorRouterActor(
                                _settingsMock.Object,
                                _pluginMock.Object,
                                _stateManagerMock.Object,
                                _eventStateMock.Object,
                                _serviceMock.Object))
                        .WithRouter(new SmallestMailboxPool(_settingsMock.Object.FixtureCreationConcurrency)),
                    "sport-processor-pool");

            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = "Football" });

            IActorRef streamListenerManagerActorRef;

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Once);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);

                    streamListenerManagerActorRef =
                        GetChildActorRef(
                            sportProcessorRouterActor,
                            StreamListenerManagerActor.ActorName + "ForFootball");

                    Assert.NotNull(streamListenerManagerActorRef);

                    Assert.Throws<AggregateException>(() =>
                    {
                        Sys.ActorSelection(
                                streamListenerManagerActorRef,
                                StreamListenerActor.ActorName + "For" + resourceFacadeMock.Object.Id)
                            .ResolveOne(TimeSpan.FromSeconds(5)).Wait();
                    }).InnerException.Should().BeOfType<ActorNotFoundException>();
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that on Initialization when Start Streaming on resource is not responding
        /// then message is passed to the Stream Listener Manager and the Stream Listener Child actor is stopped
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializingWhenStartStreamingIsNotRespondingThenStopStreamListenerChildActor()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 1, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            _settingsMock.SetupGet(o => o.StartStreamingTimeoutInSeconds).Returns(1);
            _settingsMock.SetupGet(o => o.StartStreamingAttempts).Returns(3);
            _serviceMock.Setup(o => o.GetResources(It.Is<string>(s => s.Equals("Football"))))
                .Returns(new List<IResourceFacade> { resourceFacadeMock.Object });
            resourceFacadeMock.Reset();
            resourceFacadeMock.Setup(o => o.Id).Returns(snapshot.Id);
            resourceFacadeMock.Setup(o => o.Sport).Returns("Football");
            resourceFacadeMock.Setup(o => o.MatchStatus).Returns((MatchStatus)Convert.ToInt32(snapshot.MatchStatus));
            resourceFacadeMock.Setup(o => o.Content).Returns(new Summary
            {
                Id = snapshot.Id,
                Sequence = snapshot.Sequence,
                MatchStatus = Convert.ToInt32(snapshot.MatchStatus),
                StartTime = snapshot.StartTime?.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });

            //
            //Act
            //
            var sportProcessorRouterActor =
                ActorOfAsTestActorRef<SportProcessorRouterActor>(
                    Props.Create(() =>
                            new SportProcessorRouterActor(
                                _settingsMock.Object,
                                _pluginMock.Object,
                                _stateManagerMock.Object,
                                _eventStateMock.Object,
                                _serviceMock.Object))
                        .WithRouter(new SmallestMailboxPool(_settingsMock.Object.FixtureCreationConcurrency)),
                    "sport-processor-pool");

            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = "Football" });
            //wait for 5 seconds while the health checks go every seconds and after 3 attempts the manager kills the actor
            Task.Delay(5000).Wait();
            IActorRef streamListenerManagerActorRef;

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);

                    streamListenerManagerActorRef =
                        GetChildActorRef(
                            sportProcessorRouterActor,
                            StreamListenerManagerActor.ActorName + "ForFootball");

                    Assert.NotNull(streamListenerManagerActorRef);

                    Assert.Throws<AggregateException>(() =>
                    {
                        Sys.ActorSelection(
                                streamListenerManagerActorRef,
                                StreamListenerActor.ActorName + "For" + resourceFacadeMock.Object.Id)
                            .ResolveOne(TimeSpan.FromSeconds(5)).Wait();
                    }).InnerException.Should().BeOfType<ActorNotFoundException>();
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that when disconnection occurs then the reconnection is automatically done.
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnDisconnectionEnsureReconnectWhenMatchInPlay()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/"Football",
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            _serviceMock.Setup(o => o.GetResources(It.Is<string>(s => s.Equals("Football"))))
                .Returns(new List<IResourceFacade> { resourceFacadeMock.Object });

            var sportProcessorRouterActor =
                ActorOfAsTestActorRef<SportProcessorRouterActor>(
                    Props.Create(() =>
                            new SportProcessorRouterActor(
                                _settingsMock.Object,
                                _pluginMock.Object,
                                _stateManagerMock.Object,
                                _eventStateMock.Object,
                                _serviceMock.Object))
                        .WithRouter(new SmallestMailboxPool(_settingsMock.Object.FixtureCreationConcurrency)),
                    "sport-processor-pool");

            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = "Football" });

            IActorRef streamListenerManagerActorRef = null;
            IActorRef streamListenerActorRef = null;
            StreamListenerActor streamListenerActor = null;
            IActorRef resourceActorRef = null;

            //Get child actors instances
            AwaitAssert(() =>
                {
                    streamListenerManagerActorRef = streamListenerActorRef =
                        GetChildActorRef(
                            sportProcessorRouterActor,
                            StreamListenerManagerActor.ActorName + "ForFootball");
                    streamListenerActorRef =
                        GetChildActorRef(
                            streamListenerManagerActorRef,
                            StreamListenerActor.ActorName + "For" + resourceFacadeMock.Object.Id);
                    streamListenerActor = GetUnderlyingActor<StreamListenerActor>(streamListenerActorRef);
                    resourceActorRef = GetChildActorRef(streamListenerActorRef, ResourceActor.ActorName);
                    Assert.NotNull(streamListenerActorRef);
                    Assert.NotNull(streamListenerActor);
                    Assert.NotNull(resourceActorRef);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            //
            //Act
            //
            //Wait 1 second and force Stream Disconnection
            Task.Delay(1000).Wait();
            resourceActorRef.Tell(
                new ResourceStopStreamingMsg(),
                streamListenerManagerActorRef);

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    streamListenerManagerActorRef = streamListenerActorRef =
                        GetChildActorRef(
                            sportProcessorRouterActor,
                            StreamListenerManagerActor.ActorName + "ForFootball");
                    streamListenerActorRef =
                        GetChildActorRef(
                            streamListenerManagerActorRef,
                            StreamListenerActor.ActorName + "For" + resourceFacadeMock.Object.Id);
                    streamListenerActor = GetUnderlyingActor<StreamListenerActor>(streamListenerActorRef);
                    resourceActorRef = GetChildActorRef(streamListenerActorRef, ResourceActor.ActorName);

                    Assert.NotNull(streamListenerActorRef);
                    Assert.NotNull(streamListenerActor);
                    Assert.NotNull(resourceActorRef);

                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    resourceFacadeMock.Verify(a => a.StopStreaming(), Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    _pluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    _pluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessFixtureDeletion(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, streamListenerActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        #endregion

        #region Private methods

        private void SetupCommonMockObjects(
            string sport,
            byte[] fixtureData,
            dynamic storedData,
            out Fixture snapshot,
            out Mock<IResourceFacade> resourceFacadeMock,
            Action<Mock<IResourceFacade>, string> resourceGetSnapshotCallsSequence = null)
        {
            resourceFacadeMock = new Mock<IResourceFacade>();

            var snapshotJson = System.Text.Encoding.UTF8.GetString(fixtureData);
            snapshot = FixtureJsonHelper.GetFromJson(snapshotJson);
            var snapshotVar = snapshot;
            resourceFacadeMock.Setup(o => o.Id).Returns(snapshot.Id);
            resourceFacadeMock.Setup(o => o.Sport).Returns(sport);
            resourceFacadeMock.Setup(o => o.MatchStatus).Returns((MatchStatus)Convert.ToInt32(snapshot.MatchStatus));
            resourceFacadeMock.Setup(o => o.Content).Returns(new Summary
            {
                Id = snapshot.Id,
                Sequence = snapshot.Sequence,
                MatchStatus = Convert.ToInt32(snapshot.MatchStatus),
                StartTime = snapshot.StartTime?.ToString("yyyy-MM-ddTHH:mm:ssZ")
            });
            if (resourceGetSnapshotCallsSequence == null)
            {
                resourceFacadeMock.Setup(o => o.GetSnapshot()).Returns(snapshotJson);
            }
            else
            {
                resourceGetSnapshotCallsSequence(resourceFacadeMock, snapshotJson);
            }
            resourceFacadeMock.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resourceFacadeMock.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            _stateManagerMock.Setup(o => o.CreateNewMarketRuleManager(It.Is<string>(id => id.Equals(snapshotVar.Id))))
                .Returns(new Mock<IMarketRulesManager>().Object);
            _stateProvider = new Mock<IStateProvider>();
            _suspensionManager = new Mock<ISuspensionManager>();
            _stateManagerMock.SetupGet(o => o.StateProvider)
                .Returns(_stateProvider.Object);
            _stateProvider.SetupGet(o => o.SuspensionManager)
                .Returns(_suspensionManager.Object);

            var storedFixtureState = new FixtureState { Id = snapshot.Id };

            if (storedData != null)
            {
                storedFixtureState.Epoch = (int)storedData.Epoch;
                storedFixtureState.Sequence = (int)storedData.Sequence;
                storedFixtureState.MatchStatus = (MatchStatus)storedData.MatchStatus;
                _eventStateMock.Setup(o => o.GetFixtureState(It.Is<string>(id => id.Equals(snapshotVar.Id))))
                    .Returns(storedFixtureState);
            }
            _eventStateMock.Setup(o =>
                o.UpdateFixtureState(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<MatchStatus>(),
                    It.IsAny<int>())).Callback<string, string, int, MatchStatus, int>(
                (sportParam, fixtureId, sequence, matchStatus, epoch) =>
                {
                    storedFixtureState.Sport = sportParam;
                    storedFixtureState.Id = fixtureId;
                    storedFixtureState.Sequence = sequence;
                    storedFixtureState.MatchStatus = matchStatus;
                    storedFixtureState.Epoch = epoch;
                });
        }

        #endregion
    }
}

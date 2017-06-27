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
using Akka.TestKit.NUnit;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    /// <summary>
    /// TODO: REMOVE WAITS FROM EACH TEST AND REPLACE WITH MESSAGE REPLY FROM THE ACTOR
    /// </summary>
    [TestFixture]
    public class StreamListenerActorTests : TestKit
    {
        #region Constants

        public const int ASSERT_WAIT_TIMEOUT = 2500 /*ms*/;
        public const int ASSERT_EXEC_INTERVAL = 250 /*ms*/;

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
            _settingsMock.SetupGet(a => a.StreamSafetyThreshold).Returns(5);

            _serviceMock = new Mock<IServiceFacade>();

            _eventStateMock = new Mock<IEventState>();

            _stateManagerMock = new Mock<IStateManager>();

            AdapterActorSystem.Init(_settingsMock.Object, _serviceMock.Object, Sys, false);
        }

        #endregion

        #region Test Methods

        [Test]
        [Category("StreamListenerActor")]
        public void OnInitializationStartStreamingAndProcessFirstSnapshotWhenMatchStatusNotReadyNorSetup()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_1,
                /*storedMatchStatus*/MatchStatus.InRunning,
                /*storedSequence*/1,
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

        [Test]
        [Category("StreamListenerActor")]
        public void OnInitializationMoveToFinishedStateWhenResourceHasMatchOverStatus()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_matchover_snapshot_1,
                /*storedMatchStatus*/MatchStatus.InRunning,
                /*storedSequence*/1,
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
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)), SuspensionReason.SUSPENSION),
                        Times.Once);
                    _stateManagerMock.Verify(a =>
                            a.ClearState(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Finished, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        [Category("StreamListenerActor")]
        public void OnInitializationMoveToFinishedStateWhenMatchOverWasAlreadyProcessed()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_matchover_snapshot_1,
                /*storedMatchStatus*/MatchStatus.MatchOver,
                /*storedSequence*/1,
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
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)), SuspensionReason.SUSPENSION),
                        Times.Never);
                    _stateManagerMock.Verify(a =>
                            a.ClearState(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Finished, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        [Category("StreamListenerActor")]
        public void OnInitializationProcessFullSnapshotWhenCurrentMatchStatusIsDifferentThanStoredMatchStatus()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_1,
                /*storedMatchStatus*/MatchStatus.Prematch,
                /*storedSequence*/1,
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
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)), SuspensionReason.SUSPENSION),
                        Times.Never);
                    _stateManagerMock.Verify(a =>
                            a.ClearState(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        [Category("StreamListenerActor")]
        public void OnInitializationAfterStartStreamingUnSuspendFixtureOnProcessSnapshotWhenSequenceHasNotChanged()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_1,
                /*storedMatchStatus*/MatchStatus.InRunning,
                /*storedSequence*/2,
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

        [Test]
        [Category("StreamListenerActor")]
        public void OnInitializationStartStreamingWhenMatchStatusReady()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_ready_snapshot_2,
                /*storedMatchStatus*/MatchStatus.Ready,
                /*storedSequence*/1,
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

        [Test]
        [Category("StreamListenerActor")]
        public void OnInitializationMoveToInitializedStateWhenMatchStatusSetupWithNotAllowStreamingInSetupMode()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_setup_snapshot_1,
                /*storedMatchStatus*/MatchStatus.Setup,
                /*storedSequence*/1,
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
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Initialized, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        [Category("StreamListenerActor")]
        public void ProcessSnapshotOnlyOnceWhenMovingFromInitializedStateToStreamingState()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_setup_snapshot_1,
                /*storedMatchStatus*/MatchStatus.Setup,
                /*storedSequence*/1,
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
                MatchStatus = (int)MatchStatus.Ready
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
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
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
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)), SuspensionReason.SUSPENSION),
                        Times.Never);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        [Category("StreamListenerActor")]
        public void ProcessSnapshotOnUpdateMessageNotDoneWhenSequenceInvalidLowerThanCurrentSequence()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_1,
                /*storedMatchStatus*/MatchStatus.InRunning,
                /*storedSequence*/1,
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
                        a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)), SuspensionReason.SUSPENSION),
                    Times.Never);
                Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
            },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        [Category("StreamListenerActor")]
        public void ProcessSnapshotOnUpdateMessageWhenSequenceInvalidMoreThan1GreaterThanCurrentSequence()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_1,
                /*storedMatchStatus*/MatchStatus.InRunning,
                /*storedSequence*/1,
                out snapshot,
                out resourceFacadeMock);
            var update = new Fixture
            {
                Id = resourceFacadeMock.Object.Id,
                //invalid sequence
                Sequence = resourceFacadeMock.Object.Content.Sequence + 2,
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
                    Times.Never);
                _pluginMock.Verify(a =>
                        a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                    Times.Never);
                _suspensionManager.Verify(a =>
                        a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                    Times.Never);
                _suspensionManager.Verify(a =>
                        a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)), SuspensionReason.SUSPENSION),
                    Times.Once);
                Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
            },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        [Test]
        [Category("StreamListenerActor")]
        public void ProcessEpochChangeWhenMatchStatusChange()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*fixtureData*/FixtureSamples.football_ready_snapshot_2,
                /*storedMatchStatus*/MatchStatus.Ready,
                /*storedSequence*/1,
                out snapshot,
                out resourceFacadeMock);
            var update = new Fixture
            {
                Id = resourceFacadeMock.Object.Id,
                //invalid sequence
                Sequence = resourceFacadeMock.Object.Content.Sequence + 1,
                Epoch = snapshot.Epoch + 1,
                MatchStatus = ((int)MatchStatus.InRunning).ToString(),
                LastEpochChangeReason = new[] {(int) EpochChangeReason.MatchStatus}
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

            //After initially processing first snapshot we now need to increment mock snapshot sequence before applying update message
            snapshot.Sequence += 1;

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
                        Times.Never);
                    _pluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    _suspensionManager.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)), SuspensionReason.SUSPENSION),
                        Times.Once);
                    Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        #endregion

        #region Private methods

        private void SetupCommonMockObjects(
            byte[] fixtureData,
            MatchStatus storedMatchStatus,
            int storedSequence,
            out Fixture snapshot,
            out Mock<IResourceFacade> resourceFacadeMock)
        {
            resourceFacadeMock = new Mock<IResourceFacade>();

            var snapshotJson = System.Text.Encoding.UTF8.GetString(fixtureData);
            snapshot = FixtureJsonHelper.GetFromJson(snapshotJson);
            var snapshotVar = snapshot;
            var storedFixtureState =
                new FixtureState
                {
                    Id = snapshot.Id,
                    Sequence = storedSequence,
                    MatchStatus = storedMatchStatus
                };
            resourceFacadeMock.Setup(o => o.Id).Returns(snapshot.Id);
            resourceFacadeMock.Setup(o => o.MatchStatus).Returns((MatchStatus)Convert.ToInt32(snapshot.MatchStatus));
            resourceFacadeMock.Setup(o => o.Content).Returns(new Summary { Sequence = snapshot.Sequence });
            resourceFacadeMock.Setup(o => o.GetSnapshot()).Returns(snapshotJson);
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
            _eventStateMock.Setup(o => o.GetFixtureState(It.Is<string>(id => id.Equals(snapshotVar.Id))))
                .Returns(storedFixtureState);
            _eventStateMock.Setup(o =>
                o.UpdateFixtureState(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<MatchStatus>(),
                    It.IsAny<int>())).Callback<string, string, int, MatchStatus, int>(
                (sport, fixtureId, sequence, matchStatus, epoch) =>
                {
                    storedFixtureState.Sport = sport;
                    storedFixtureState.Id = fixtureId;
                    storedFixtureState.Sequence = sequence;
                    storedFixtureState.MatchStatus = matchStatus;
                    storedFixtureState.Epoch = epoch;
                });
        }

        #endregion
    }
}

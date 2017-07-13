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
using Akka.Actor;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using Akka.Routing;
using SS.Integration.Adapter.Enums;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamHealthCheckActorTests : BaseTestKit
    {
        #region Constants

        public const int ASSERT_WAIT_TIMEOUT = 5000 /*ms*/;
        public const int ASSERT_EXEC_INTERVAL = 200 /*ms*/;
        public const string STREAM_HEALTH_CHECK_ACTOR_CATEGORY = nameof(StreamHealthCheckActor);

        #endregion

        #region Attributes

        private Mock<ISettings> _settingsMock;
        private Mock<IAdapterPlugin> _pluginMock;
        private Mock<IServiceFacade> _serviceMock;
        private Mock<IEventState> _eventStateMock;
        private Mock<IStateManager> _stateManagerMock;
        private Mock<IStateProvider> _stateProvider;
        private Mock<ISuspensionManager> _suspensionManager;
        private Mock<IStreamValidation> _streamValidationMock;
        private Mock<IFixtureValidation> _fixtureValidationMock;

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
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures we move to Streaming State after processing health check message.
        /// Also Snapshot processing is done twice 
        /// - first snapshot processing is done on initialization due to different sequence numbers between stored and current
        /// - second snapshot processing is done after we process the health check message due to match status changed
        /// </summary>
        [Test]
        [Category(STREAM_HEALTH_CHECK_ACTOR_CATEGORY)]
        public void OnHealthCheckConnectToStreamingServerAndProcessSnapshot()
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
            _serviceMock.Setup(o => o.GetResources(It.Is<string>(s => s.Equals("Football"))))
                .Returns(new List<IResourceFacade> { resourceFacadeMock.Object });
            _streamValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(false);
            _settingsMock.SetupGet(a => a.AllowFixtureStreamingInSetupMode).Returns(false);
            _fixtureValidationMock.Setup(a =>
                    a.IsSnapshotNeeded(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<FixtureState>()))
                .Returns(true);

            //
            //Act
            //
            var streamListenerManagerActor =
                ActorOfAsTestActorRef<StreamListenerManagerActor>(
                    Props.Create(() =>
                        new StreamListenerManagerActor(
                            _settingsMock.Object,
                            _pluginMock.Object,
                            _stateManagerMock.Object,
                            _eventStateMock.Object,
                            _streamValidationMock.Object,
                            _fixtureValidationMock.Object)),
                    StreamListenerManagerActor.ActorName);
            var sportProcessorRouterActor =
                ActorOfAsTestActorRef<SportProcessorRouterActor>(
                    Props.Create(() => new SportProcessorRouterActor(_serviceMock.Object))
                        .WithRouter(new SmallestMailboxPool(_settingsMock.Object.FixtureCreationConcurrency)),
                    SportProcessorRouterActor.ActorName);

            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = "Football" });

            IActorRef streamListenerActorRef;
            StreamListenerActor streamListenerActor = null;
            IActorRef resourceActorRef;
            IActorRef healthCheckActorRef;

            //Get child actors instances
            AwaitAssert(() =>
                {
                    streamListenerActorRef =
                        GetChildActorRef(
                            streamListenerManagerActor,
                            StreamListenerActor.GetName(resourceFacadeMock.Object.Id));
                    streamListenerActor = GetUnderlyingActor<StreamListenerActor>(streamListenerActorRef);
                    resourceActorRef = GetChildActorRef(streamListenerActorRef, ResourceActor.ActorName);
                    healthCheckActorRef = GetChildActorRef(streamListenerActorRef, StreamHealthCheckActor.ActorName);
                    Assert.NotNull(streamListenerActorRef);
                    Assert.NotNull(streamListenerActor);
                    Assert.NotNull(resourceActorRef);
                    Assert.NotNull(healthCheckActorRef);

                    Assert.AreEqual(StreamListenerState.Initialized, streamListenerActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            _streamValidationMock.Reset();
            _streamValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            _streamValidationMock.Setup(a =>
                    a.ValidateStream(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>(),
                        It.IsAny<int>()))
                .Returns(true);
            //This call will trigger health check message
            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = "Football" });
            System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(30)).Wait();
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
                    Assert.AreEqual(StreamListenerState.Streaming, streamListenerActor.State);
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

            _streamValidationMock = new Mock<IStreamValidation>();
            _fixtureValidationMock = new Mock<IFixtureValidation>();
        }

        #endregion
    }
}

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
using System.Threading.Tasks;
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
using FluentAssertions;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Enums;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamHealthCheckActorTests : AdapterTestKit
    {
        #region Constants

        public const string STREAM_HEALTH_CHECK_ACTOR_CATEGORY = nameof(StreamHealthCheckActor);

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            PluginMock = new Mock<IAdapterPlugin>();

            SettingsMock = new Mock<ISettings>();
            SettingsMock.SetupGet(a => a.StreamSafetyThreshold).Returns(3);
            SettingsMock.SetupGet(a => a.FixtureCreationConcurrency).Returns(3);
            SettingsMock.SetupGet(a => a.FixtureCheckerFrequency).Returns(10000);
            SettingsMock.SetupGet(a => a.FixturesStateFilePath).Returns(GetType().Assembly.Location);
            SettingsMock.SetupGet(a => a.FixturesStateAutoStoreInterval).Returns(int.MaxValue);

            FootabllSportMock = new Mock<IFeature>();
            ServiceMock = new Mock<IServiceFacade>();
            StateManagerMock = new Mock<IStateManager>();
            MarketRulesManagerMock = new Mock<IMarketRulesManager>();
            StateProviderMock = new Mock<IStateProvider>();
            StoreProviderMock = new Mock<IStoreProvider>();
            SuspensionManagerMock = new Mock<ISuspensionManager>();
            StreamHealthCheckValidationMock = new Mock<IStreamHealthCheckValidation>();
            FixtureValidationMock = new Mock<IFixtureValidation>();
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
        public void ConnectToStreamingServerAndProcessSnapshot()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_setup_snapshot_2,
                /*storedData*/new { Epoch = 3, Sequence = 1, MatchStatus = MatchStatus.Setup },
                out snapshot,
                out resourceFacadeMock);
            ServiceMock.Setup(o => o.GetResources(It.Is<string>(s => s.Equals(FootabllSportMock.Object.Name))))
                .Returns(new List<IResourceFacade> { resourceFacadeMock.Object });
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(false);
            SettingsMock.SetupGet(a => a.AllowFixtureStreamingInSetupMode).Returns(false);
            FixtureValidationMock.Setup(a =>
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
                            SettingsMock.Object,
                            PluginMock.Object,
                            StateManagerMock.Object,
                            StreamHealthCheckValidationMock.Object,
                            FixtureValidationMock.Object)),
                    StreamListenerManagerActor.ActorName);
            var sportProcessorRouterActor =
                ActorOfAsTestActorRef<SportProcessorRouterActor>(
                    Props.Create(() => new SportProcessorRouterActor(ServiceMock.Object))
                        .WithRouter(new SmallestMailboxPool(SettingsMock.Object.FixtureCreationConcurrency)),
                    SportProcessorRouterActor.ActorName);

            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = FootabllSportMock.Object.Name });

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

            StreamHealthCheckValidationMock.Reset();
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.ValidateStream(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>(),
                        It.IsAny<int>()))
                .Returns(true);
            //This call will trigger health check message
            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = FootabllSportMock.Object.Name });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Exactly(2));
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Exactly(2));
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, streamListenerActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that when trying to connect to the streaming server and that is not responding
        /// then the Stream Listener Child actor is stopped by the Stream Listener Manager
        /// </summary>
        [Test]
        [Category(STREAM_HEALTH_CHECK_ACTOR_CATEGORY)]
        public void WhenStartStreamingIsNotRespondingThenStopStreamListener()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 1, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            SettingsMock.SetupGet(o => o.StartStreamingTimeoutInSeconds).Returns(1);
            SettingsMock.SetupGet(o => o.StartStreamingAttempts).Returns(3);
            ServiceMock.Setup(o => o.GetResources(It.Is<string>(s => s.Equals(FootabllSportMock.Object.Name))))
                .Returns(new List<IResourceFacade> { resourceFacadeMock.Object });
            resourceFacadeMock.Reset();
            resourceFacadeMock.Setup(o => o.Id).Returns(snapshot.Id);
            resourceFacadeMock.Setup(o => o.Sport).Returns(FootabllSportMock.Object.Name);
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
            var streamListenerManagerActor =
                ActorOfAsTestActorRef<StreamListenerManagerActor>(
                    Props.Create(() =>
                        new StreamListenerManagerActor(
                            SettingsMock.Object,
                            PluginMock.Object,
                            StateManagerMock.Object,
                            StreamHealthCheckValidationMock.Object,
                            FixtureValidationMock.Object)),
                    StreamListenerManagerActor.ActorName);
            var sportProcessorRouterActor =
                ActorOfAsTestActorRef<SportProcessorRouterActor>(
                    Props.Create(() => new SportProcessorRouterActor(ServiceMock.Object))
                        .WithRouter(new SmallestMailboxPool(SettingsMock.Object.FixtureCreationConcurrency)),
                    SportProcessorRouterActor.ActorName);

            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = FootabllSportMock.Object.Name });

            //wait for 5 seconds while the health checks run every second and after 3 attempts the manager kills the actor
            Task.Delay(5000).Wait();

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);

                    Assert.Throws<AggregateException>(() =>
                    {
                        Sys.ActorSelection(
                                streamListenerManagerActor,
                                StreamListenerActor.GetName(resourceFacadeMock.Object.Id))
                            .ResolveOne(TimeSpan.FromSeconds(5)).Wait();
                    }).InnerException.Should().BeOfType<ActorNotFoundException>();
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        #endregion
    }
}

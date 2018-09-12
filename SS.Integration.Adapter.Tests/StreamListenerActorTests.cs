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
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
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
using SportingSolutions.Udapi.Sdk;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Configuration;
using SS.Integration.Adapter.Diagnostics.Model.Service.Model;
using SS.Integration.Adapter.Enums;
using Settings = SS.Integration.Adapter.Configuration.Settings;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamListenerActorTests : AdapterTestKit
    {
        #region Constants

        public const string STREAM_LISTENER_ACTOR_CATEGORY = nameof(StreamListenerActor);

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            SetupTestLogging();

            PluginMock = new Mock<IAdapterPlugin>();

            SettingsMock = new Mock<ISettings>();
            SettingsMock.SetupGet(a => a.StreamSafetyThreshold).Returns(3);
            SettingsMock.SetupGet(a => a.FixtureCreationConcurrency).Returns(3);
            SettingsMock.SetupGet(a => a.FixtureCheckerFrequency).Returns(10000);
            SettingsMock.SetupGet(a => a.StateProviderPath).Returns(GetType().Assembly.Location);
            SettingsMock.SetupGet(a => a.FixturesStateFilePath).Returns(GetType().Assembly.Location);
            SettingsMock.SetupGet(a => a.FixturesStateAutoStoreInterval).Returns(int.MaxValue);

            FootabllSportMock = new Mock<IFeature>();
            FootabllSportMock.SetupGet(o => o.Name).Returns("Football");
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
        /// This test ensures we process the first snapshot on initialization,
        /// also we start streaming when it is the case (for this test status is InPlay so we should start streaming).
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationStartStreamingAndProcessFirstSnapshot()
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
            FixtureValidationMock.Setup(a =>
                    a.IsSnapshotNeeded(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<FixtureState>()))
                .Returns(true);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we process the match over when status has changed from InPlay to MatchOver
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationMoveToStoppedStateWhenResourceHasMatchOverStatus()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_matchover_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 1, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once); //because we make unsuspend in every snapshot processing if fixture had been suspended
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.MATCH_OVER),
                        Times.Once);
                    StateManagerMock.Verify(a =>
                            a.ClearState(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Stopped, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we move directly to finished state on initialization when the fixture has status match over on the saved state
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationMoveToStoppedStateWhenMatchOverWasAlreadyProcessed()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_matchover_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 1, MatchStatus = MatchStatus.MatchOver },
                out snapshot,
                out resourceFacadeMock);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Never);
                    StateManagerMock.Verify(a =>
                            a.ClearState(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Stopped, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 1, MatchStatus = MatchStatus.Prematch },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            FixtureValidationMock.Setup(a =>
                    a.IsSnapshotNeeded(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<FixtureState>()))
                .Returns(true);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Never);
                    StateManagerMock.Verify(a =>
                            a.ClearState(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_ready_snapshot_3,
                /*storedData*/new { Epoch = 3, Sequence = 1, MatchStatus = MatchStatus.Ready },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            FixtureValidationMock.Setup(a =>
                    a.IsSnapshotNeeded(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<FixtureState>()))
                .Returns(true);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we move to Initialized state when Fixture Status in Setup and Streaming in Setup is disabled
        /// Also First Snapshot is processed as being for the first time.
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_setup_snapshot_2,
                /*storedData*/new { Epoch = 3, Sequence = 2, MatchStatus = MatchStatus.Setup },
                out snapshot,
                out resourceFacadeMock);
            SettingsMock.SetupGet(a => a.AllowFixtureStreamingInSetupMode).Returns(false);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Initialized, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we start streaming and unsuspend the fixture when snapshot is not needed
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnInitializationEnsureStartStreamingAndUnSuspendFixtureWhenSnapshotNotNeeded()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
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
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });
            Task.Delay(10000).Wait();
            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));  //because first unsuspend when start streaming, second when processing snapshot for invalid sequence
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SNAPSHOT),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        ///// <summary>
        ///// If test is failed check maxFixtureUpdateDelayInSeconds and delayedFixtureRecoveryAttemptSchedule
        ///// </summary>
        //[Test]
        //[Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        //public void OnValidationFixtureTimeStampFailedWhenProcessSnapshot()
        //{
        //    //
        //    //Arrange
        //    //
        //    Fixture snapshot;
        //    Mock<IResourceFacade> resourceFacadeMock;
        //    var now = DateTime.UtcNow;
        //    int maxFixtureUpdateDelayInSeconds = 60;
        //    int delayedFixtureRecoveryAttemptSchedule = 10;
        //    SetupCommonMockObjects(
        //        /*sport*/FootabllSportMock.Object.Name,
        //        /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
        //        /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
        //        out snapshot,
        //        out resourceFacadeMock);

        //    SettingsMock.SetupGet(a => a.maxFixtureUpdateDelayInSeconds).Returns(maxFixtureUpdateDelayInSeconds);
        //    StreamHealthCheckValidationMock.Setup(a =>
        //            a.CanConnectToStreamServer(
        //                It.IsAny<IResourceFacade>(),
        //                It.IsAny<StreamListenerState>()))
        //        .Returns(true);
        //    FixtureValidationMock.Setup(a =>
        //            a.IsSequenceValid(
        //                It.IsAny<Fixture>(),
        //                It.IsAny<int>()))
        //        .Returns(true);
        //    FixtureValidationMock.Setup(a =>
        //            a.IsEpochValid(
        //                It.IsAny<Fixture>(),
        //                It.IsAny<int>()))
        //        .Returns(true);


        //    var update = new Fixture
        //    {
        //        Id = resourceFacadeMock.Object.Id,
        //        Epoch = 7,
        //        //invalid sequence
        //        Sequence = resourceFacadeMock.Object.Content.Sequence + 1,
        //        MatchStatus = ((int)resourceFacadeMock.Object.MatchStatus).ToString(),
        //        TimeStamp = now.Subtract(TimeSpan.FromSeconds(SettingsMock.Object.maxFixtureUpdateDelayInSeconds + 1))
        //    };
        //    StreamMessage message = new StreamMessage { Content = update };

        //    //
        //    //Act
        //    //
        //    var actor = ActorOfAsTestActorRef(() =>
        //        new StreamListenerActor(
        //            SettingsMock.Object,
        //            PluginMock.Object,
        //            resourceFacadeMock.Object,
        //            StateManagerMock.Object,
        //            SuspensionManagerMock.Object,
        //            StreamHealthCheckValidationMock.Object,
        //            FixtureValidationMock.Object));
        //    actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });

        //    Task.Delay((delayedFixtureRecoveryAttemptSchedule + 2) * 1000).Wait();

        //    AwaitAssert(() =>
        //    {
        //        SuspensionManagerMock.Verify(a =>
        //                a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
        //                    SuspensionReason.UPDTATE_DELAYED),
        //            Times.Once);  //handleupdatedelay
        //        resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
        //        //PluginMock.Verify(a =>
        //        //        a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
        //        //    Times.Once);
        //        SuspensionManagerMock.Verify(a =>
        //                a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
        //            Times.Never);
        //        SuspensionManagerMock.Verify(a =>
        //                a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
        //                    SuspensionReason.SUSPENSION),
        //            Times.Never);
        //        StateManagerMock.Verify(a =>
        //                a.ClearState(It.Is<string>(id => id.Equals(resourceFacadeMock.Object.Id))),
        //            Times.Never);
        //        MarketRulesManagerMock.Verify(a =>
        //                a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
        //            Times.Once);
        //        MarketRulesManagerMock.Verify(a =>
        //                a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
        //            Times.Never);
        //        MarketRulesManagerMock.Verify(a =>
        //                a.CommitChanges(),
        //            Times.Once);
        //        MarketRulesManagerMock.Verify(a =>
        //                a.RollbackChanges(),
        //            Times.Never);
        //        Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
        //    },
        //        TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
        //        TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        //}



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
                /*sport*/FootabllSportMock.Object.Name,
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
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            FixtureValidationMock.Setup(a =>
                    a.IsSnapshotNeeded(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<FixtureState>()))
                .Returns(true);
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
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            resourceFacadeMock.Object.Content.Sequence = update.Sequence;
            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });
            Task.Delay(10000).Wait();
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
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);    //because process snapshot unsuspends fixture
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SNAPSHOT),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Exactly(2));
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_ready_snapshot_3,
                /*storedData*/new { Epoch = 3, Sequence = 2, MatchStatus = MatchStatus.Ready },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            FixtureValidationMock.Setup(a =>
                    a.IsSnapshotNeeded(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<FixtureState>()))
                .Returns(true);
            FixtureValidationMock.Setup(a =>
                    a.IsSequenceValid(
                        It.IsAny<Fixture>(),
                        It.IsAny<int>()))
                .Returns(true);
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
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });
            Task.Delay(10000).Wait();
            //
            //Assert
            //
            AwaitAssert(() =>
                {
                   resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.AtLeast(1));
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);   //because once calling of process snapshot calls unsuspend
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SNAPSHOT),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Exactly(2));
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            FixtureValidationMock.Setup(a =>
                    a.IsSequenceValid(
                        It.IsAny<Fixture>(),
                        It.IsAny<int>()))
                .Returns(true);
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
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });
            Task.Delay(10000).Wait();
            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2)); //because first unsuspend when start streaming, second when processing snapshot for invalid epoch
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SNAPSHOT),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            FixtureValidationMock.Setup(a =>
                    a.IsSequenceValid(
                        It.IsAny<Fixture>(),
                        It.IsAny<int>()))
                .Returns(true);
            FixtureValidationMock.Setup(a =>
                    a.IsEpochValid(
                        It.IsAny<Fixture>(),
                        It.IsAny<int>()))
                .Returns(true);
            var update = new Fixture
            {
                Id = resourceFacadeMock.Object.Id,
                Sequence = resourceFacadeMock.Object.Content.Sequence + 1,
                Epoch = snapshot.Epoch,
                MatchStatus = ((int)MatchStatus.InRunning).ToString(),
                TimeStamp = DateTime.Now
            };
            StreamMessage message = new StreamMessage { Content = update };

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });

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
                        Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.SUSPENSION),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_ready_snapshot_3,
                /*storedData*/new { Epoch = 3, Sequence = 2, MatchStatus = MatchStatus.Ready },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            FixtureValidationMock.Setup(a =>
                    a.IsSnapshotNeeded(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<FixtureState>()))
                .Returns(true);
            FixtureValidationMock.Setup(a =>
                    a.IsSequenceValid(
                        It.IsAny<Fixture>(),
                        It.IsAny<int>()))
                .Returns(true);
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
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            actor.Tell(new StreamUpdateMsg { Data = JsonConvert.SerializeObject(message) });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    resourceFacadeMock.Verify(a => a.StopStreaming(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessFixtureDeletion(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_DELETED),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.OnFixtureUnPublished(),
                        Times.Once);
                    Assert.AreEqual(StreamListenerState.Stopped, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));
            actor.Tell(new StreamUpdateMsg { Data = "This is a JSON message that will throw error on parsing" });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.INTERNALERROR),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock,
                (mockObj, snapshotJson) =>
                {
                    mockObj.SetupSequence(o => o.GetSnapshot())
                        .Throws<System.Net.WebException>();
                });
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            AwaitAssert(() =>
                {
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.INTERNALERROR),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Errored, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
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
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
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
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            AwaitAssert(() =>
                {
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.INTERNALERROR),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Errored, actor.UnderlyingActor.State);
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
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.INTERNALERROR),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
           
            //this will force initialization to fail
            resourceFacadeMock.Reset();
            resourceFacadeMock.Setup(o => o.Id).Returns(snapshot.Id);
            ServiceMock.Setup(o => o.GetSports()).Returns(new[] { FootabllSportMock.Object });
            ServiceMock.Setup(o => o.GetResources(It.Is<string>(s => s.Equals(FootabllSportMock.Object.Name))))
                .Returns(new List<IResourceFacade> { resourceFacadeMock.Object });
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
                            SuspensionManagerMock.Object,
                            StreamHealthCheckValidationMock.Object,
                            FixtureValidationMock.Object)),
                    StreamListenerManagerActor.ActorName);
            var sportProcessorRouterActor =
                ActorOfAsTestActorRef<SportProcessorRouterActor>(
                    Props.Create(() => new SportProcessorRouterActor(ServiceMock.Object))
                        .WithRouter(new SmallestMailboxPool(SettingsMock.Object.FixtureCreationConcurrency)),
                    SportProcessorRouterActor.ActorName);
            
            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = FootabllSportMock.Object.Name });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.INTERNALERROR),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.FIXTURE_ERRORED),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
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

        /// <summary>
        /// This test ensures that we don't process the market rules when processing snapshot on-demand
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnProcessSnapshotSkipMarketRulesWhenRetrieveAndProcessSnapshotRequested()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            //
            //Act
            //
            actor.Tell(new RetrieveAndProcessSnapshotMsg { FixtureId = resourceFacadeMock.Object.Id });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that we don't process the market rules when processing snapshot on-demand
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnProcessSnapshotSkipMarketRulesWhenErrored()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            FixtureValidationMock.SetupSequence(o =>
                    o.IsSnapshotNeeded(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<FixtureState>()))
                .Throws(new Exception())
                .Returns(false);
            SettingsMock.SetupGet(o => o.SkipRulesOnError).Returns(true);

            //
            //Act
            //
            var actor = ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    SettingsMock.Object,
                    PluginMock.Object,
                    resourceFacadeMock.Object,
                    StateManagerMock.Object,
                    SuspensionManagerMock.Object,
                    StreamHealthCheckValidationMock.Object,
                    FixtureValidationMock.Object));

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, actor.UnderlyingActor.State);
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
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);

            ServiceMock.Setup(o => o.GetSports()).Returns(new[] { FootabllSportMock.Object });
            ServiceMock.Setup(o => o.GetResources(It.Is<string>(s => s.Equals(FootabllSportMock.Object.Name))))
                .Returns(new List<IResourceFacade> { resourceFacadeMock.Object });
            StreamHealthCheckValidationMock.Setup(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true);
            StreamHealthCheckValidationMock.Setup(a =>
                    a.ShouldSuspendOnDisconnection(
                        It.IsAny<FixtureState>(),
                        It.IsAny<DateTime?>()))
                .Returns(true);

            var streamListenerManagerActor =
                ActorOfAsTestActorRef<StreamListenerManagerActor>(
                    Props.Create(() =>
                        new StreamListenerManagerActor(
                            SettingsMock.Object,
                            PluginMock.Object,
                            StateManagerMock.Object,
                            SuspensionManagerMock.Object,
                            StreamHealthCheckValidationMock.Object,
                            FixtureValidationMock.Object)),
                    StreamListenerManagerActor.ActorName);
            var sportProcessorRouterActor =
                ActorOfAsTestActorRef<SportProcessorRouterActor>(
                    Props.Create(() => new SportProcessorRouterActor(ServiceMock.Object))
                        .WithRouter(new SmallestMailboxPool(SettingsMock.Object.FixtureCreationConcurrency)),
                    SportProcessorRouterActor.ActorName);
            ActorOfAsTestActorRef<SportsProcessorActor>(
                Props.Create(() =>
                    new SportsProcessorActor(
                        SettingsMock.Object,
                        ServiceMock.Object,
                        sportProcessorRouterActor)),
                SportsProcessorActor.ActorName);

            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = FootabllSportMock.Object.Name });

            IActorRef streamListenerActorRef = null;
            StreamListenerActor streamListenerActor = null;
            IActorRef resourceActorRef = null;

            //Get child actors instances
            AwaitAssert(() =>
                {
                    streamListenerActorRef =
                        GetChildActorRef(
                            streamListenerManagerActor,
                            StreamListenerActor.GetName(resourceFacadeMock.Object.Id));
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
            Task.Delay(6000).Wait();
            resourceActorRef.Tell(
                new StopStreamingMsg(),
                streamListenerManagerActor);

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    streamListenerActorRef =
                        GetChildActorRef(
                            streamListenerManagerActor,
                            StreamListenerActor.GetName(resourceFacadeMock.Object.Id));
                    streamListenerActor = GetUnderlyingActor<StreamListenerActor>(streamListenerActorRef);
                    resourceActorRef = GetChildActorRef(streamListenerActorRef, ResourceActor.ActorName);

                    Assert.NotNull(streamListenerActorRef);
                    Assert.NotNull(streamListenerActor);
                    Assert.NotNull(resourceActorRef);

                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Never);
                    resourceFacadeMock.Verify(a => a.StopStreaming(), Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessFixtureDeletion(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Exactly(2));
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Streaming, streamListenerActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures that when stream healthcheck detects invalid sequence for the second time 
        /// then it stops the stream listener
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_ACTOR_CATEGORY)]
        public void OnHealthCheckStreamInvalidSecondTimeStopStreamListener()
        {
            //
            //Arrange
            //
            Fixture snapshot;
            Mock<IResourceFacade> resourceFacadeMock;
            SetupCommonMockObjects(
                /*sport*/FootabllSportMock.Object.Name,
                /*fixtureData*/FixtureSamples.football_inplay_snapshot_2,
                /*storedData*/new { Epoch = 7, Sequence = 2, MatchStatus = MatchStatus.InRunning },
                out snapshot,
                out resourceFacadeMock);

            ServiceMock.Setup(o => o.GetSports()).Returns(new[] { FootabllSportMock.Object });
            ServiceMock.Setup(o => o.GetResources(It.Is<string>(s => s.Equals(FootabllSportMock.Object.Name))))
                .Returns(new List<IResourceFacade> { resourceFacadeMock.Object });
            StreamHealthCheckValidationMock.SetupSequence(a =>
                    a.CanConnectToStreamServer(
                        It.IsAny<IResourceFacade>(),
                        It.IsAny<StreamListenerState>()))
                .Returns(true)
                .Returns(false);

            var streamListenerManagerActor =
                ActorOfAsTestActorRef<StreamListenerManagerActor>(
                    Props.Create(() =>
                        new StreamListenerManagerActor(
                            SettingsMock.Object,
                            PluginMock.Object,
                            StateManagerMock.Object,
                            SuspensionManagerMock.Object,
                            StreamHealthCheckValidationMock.Object,
                            FixtureValidationMock.Object)),
                    StreamListenerManagerActor.ActorName);
            var sportProcessorRouterActor =
               ActorOfAsTestActorRef<SportProcessorRouterActor>(
                    Props.Create(() => new SportProcessorRouterActor(ServiceMock.Object))
                        .WithRouter(new SmallestMailboxPool(SettingsMock.Object.FixtureCreationConcurrency)),
                    SportProcessorRouterActor.ActorName);

            ActorOfAsTestActorRef<SportsProcessorActor>(
                Props.Create(() =>
                    new SportsProcessorActor(
                        SettingsMock.Object,
                        ServiceMock.Object,
                        sportProcessorRouterActor)),
                SportsProcessorActor.ActorName);

            sportProcessorRouterActor.Tell(new ProcessSportMsg { Sport = FootabllSportMock.Object.Name });
            Task.Delay(5000).Wait();
            IActorRef streamListenerActorRef = null;
            StreamListenerActor streamListenerActor = null;

            //Get child actors instances
            AwaitAssert(() =>
                {
                    streamListenerActorRef =
                        GetChildActorRef(
                            streamListenerManagerActor,
                            StreamListenerActor.GetName(resourceFacadeMock.Object.Id));
                    streamListenerActor = GetUnderlyingActor<StreamListenerActor>(streamListenerActorRef);
                    Assert.NotNull(streamListenerActorRef);
                    Assert.NotNull(streamListenerActor);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
            Task.Delay(StreamListenerActor.CONNECT_TO_STREAM_DELAY).Wait();
            AwaitAssert(() =>
            {
                Assert.AreEqual(StreamListenerState.Streaming, streamListenerActor.State);
            },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
            //
            //Act
            //
            //Wait 1 second and force Stream Health Check message


            streamListenerActorRef.Tell(new StreamHealthCheckMsg { Resource = resourceFacadeMock.Object });
            Task.Delay((Settings.MinimalHealthcheckInterval + 1)*1000).Wait();
            streamListenerActorRef.Tell(new StreamHealthCheckMsg { Resource = resourceFacadeMock.Object });
            Task.Delay(1000).Wait();
            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    streamListenerActorRef =
                        GetChildActorRef(
                            streamListenerManagerActor,
                            StreamListenerActor.GetName(resourceFacadeMock.Object.Id));
                    streamListenerActor = GetUnderlyingActor<StreamListenerActor>(streamListenerActorRef);

                    Assert.NotNull(streamListenerActorRef);
                    Assert.NotNull(streamListenerActor);

                    resourceFacadeMock.Verify(a => a.GetSnapshot(), Times.Once);
                    resourceFacadeMock.Verify(a => a.StopStreaming(), Times.AtMost(1));
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), false),
                        Times.Once);
                    PluginMock.Verify(a =>
                            a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), true),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessMatchStatus(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    PluginMock.Verify(a =>
                            a.ProcessFixtureDeletion(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Unsuspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);    
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.DISCONNECT_EVENT),
                        Times.Never);
                    SuspensionManagerMock.Verify(a =>
                            a.Suspend(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)),
                                SuspensionReason.HEALTH_CHECK_FALURE),
                        Times.Exactly(2));
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id))),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.ApplyRules(It.Is<Fixture>(f => f.Id.Equals(resourceFacadeMock.Object.Id)), It.IsAny<bool>()),
                        Times.Never);
                    MarketRulesManagerMock.Verify(a =>
                            a.CommitChanges(),
                        Times.Once);
                    MarketRulesManagerMock.Verify(a =>
                            a.RollbackChanges(),
                        Times.Never);
                    Assert.AreEqual(StreamListenerState.Initialized, streamListenerActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        #endregion
    }
}

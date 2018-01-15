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
using System.Threading.Tasks;
using Akka.Actor;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.Enums;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamListenerBuilderActorTests : AdapterTestKit
    {
        #region Constants

        public const string STREAM_LISTENER_BUILDER_ACTOR_CATEGORY = nameof(StreamListenerBuilderActor);

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
            SettingsMock.SetupGet(a => a.FixturesStateFilePath).Returns(GetType().Assembly.Location);
            SettingsMock.SetupGet(a => a.FixturesStateAutoStoreInterval).Returns(int.MaxValue);

            StateManagerMock = new Mock<IStateManager>();
            SuspensionManagerMock = new Mock<ISuspensionManager>();
            MarketRulesManagerMock = new Mock<IMarketRulesManager>();
            StreamHealthCheckValidationMock = new Mock<IStreamHealthCheckValidation>();
            FixtureValidationMock = new Mock<IFixtureValidation>();
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures we don't create more than the pre-defined number of concurrent StreamListenerActor instances.
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_BUILDER_ACTOR_CATEGORY)]
        public void TestConcurrentStreamListenerCreationLimit()
        {
            //
            //Arrange
            //
            SettingsMock.SetupGet(a => a.FixtureCreationConcurrency).Returns(3);
            var resource1FacadeMock = new Mock<IResourceFacade>();
            var resource2FacadeMock = new Mock<IResourceFacade>();
            var resource3FacadeMock = new Mock<IResourceFacade>();
            var resource4FacadeMock = new Mock<IResourceFacade>();
            var resource5FacadeMock = new Mock<IResourceFacade>();
            resource1FacadeMock.Setup(o => o.Id).Returns("Fixture1Id");
            resource2FacadeMock.Setup(o => o.Id).Returns("Fixture2Id");
            resource3FacadeMock.Setup(o => o.Id).Returns("Fixture3Id");
            resource4FacadeMock.Setup(o => o.Id).Returns("Fixture4Id");
            resource5FacadeMock.Setup(o => o.Id).Returns("Fixture5Id");

            //
            //Act
            //
            var streamListenerBuilderActorRef =
                ActorOfAsTestActorRef<StreamListenerBuilderActor>(
                    Props.Create(() =>
                        new StreamListenerBuilderActor(
                            SettingsMock.Object,
                            new Mock<IActorContext>().Object,
                            PluginMock.Object,
                            StateManagerMock.Object,
                            SuspensionManagerMock.Object,
                            StreamHealthCheckValidationMock.Object,
                            FixtureValidationMock.Object)),
                    StreamListenerBuilderActor.ActorName);

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    Assert.AreEqual(
                        StreamListenerBuilderState.Active,
                        streamListenerBuilderActorRef.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            //
            //Act
            //
            streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = resource1FacadeMock.Object });
            Task.Delay(TimeSpan.FromMilliseconds(50));
            streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = resource2FacadeMock.Object });
            Task.Delay(TimeSpan.FromMilliseconds(50));
            streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = resource3FacadeMock.Object });
            Task.Delay(TimeSpan.FromMilliseconds(50));
            streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = resource4FacadeMock.Object });
            Task.Delay(TimeSpan.FromMilliseconds(50));
            streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = resource5FacadeMock.Object });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    Assert.AreEqual(
                        StreamListenerBuilderState.Busy,
                        streamListenerBuilderActorRef.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));

            //
            //Act
            //
            Task.Delay(TimeSpan.FromMilliseconds(50));
            streamListenerBuilderActorRef.Tell(new StreamListenerCreationCompletedMsg { FixtureId = resource1FacadeMock.Object.Id });
            Task.Delay(TimeSpan.FromMilliseconds(50));
            streamListenerBuilderActorRef.Tell(new StreamListenerCreationCompletedMsg { FixtureId = resource2FacadeMock.Object.Id });
            Task.Delay(TimeSpan.FromMilliseconds(50));
            streamListenerBuilderActorRef.Tell(new StreamListenerCreationFailedMsg { FixtureId = resource4FacadeMock.Object.Id });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    Assert.AreEqual(
                        StreamListenerBuilderState.Active,
                        streamListenerBuilderActorRef.UnderlyingActor.State);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we don't create StreamListenerActor instance when we have fixture with match over and no saved state for it.
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_BUILDER_ACTOR_CATEGORY)]
        public void TestSkipStreamListenerActorInstanceCreationWhenFixtureHasMatchOver()
        {
            //
            //Arrange
            //
            SettingsMock.SetupGet(a => a.FixtureCreationConcurrency).Returns(10);
            var resourceFacadeMock = new Mock<IResourceFacade>();
            resourceFacadeMock.SetupGet(o => o.Id).Returns("Fixture1Id");
            resourceFacadeMock.SetupGet(o => o.MatchStatus).Returns(MatchStatus.MatchOver);

            //
            //Act
            //
            var streamListenerBuilderActorRef =
                ActorOfAsTestActorRef<StreamListenerBuilderActor>(
                    Props.Create(() =>
                        new StreamListenerBuilderActor(
                            SettingsMock.Object,
                            new Mock<IActorContext>().Object,
                            PluginMock.Object,
                            StateManagerMock.Object,
                            SuspensionManagerMock.Object,
                            StreamHealthCheckValidationMock.Object,
                            FixtureValidationMock.Object)),
                    StreamListenerBuilderActor.ActorName);

            //
            //Act
            //
            streamListenerBuilderActorRef.Tell(
                new CreateStreamListenerMsg
                {
                    Resource = resourceFacadeMock.Object
                });

            Task.Delay(TimeSpan.FromMilliseconds(100));

            streamListenerBuilderActorRef.Tell(
                new CheckFixtureStateMsg
                {
                    Resource = resourceFacadeMock.Object,
                    ShouldProcessFixture = false
                });

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    Assert.AreEqual(
                        StreamListenerBuilderState.Active,
                        streamListenerBuilderActorRef.UnderlyingActor.State);
                    Assert.AreEqual(
                        0,
                        streamListenerBuilderActorRef.UnderlyingActor.CreationInProgressFixtureIdSetCount);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        #endregion
    }
}

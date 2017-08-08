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
            PluginMock = new Mock<IAdapterPlugin>();

            SettingsMock = new Mock<ISettings>();
            SettingsMock.SetupGet(a => a.StreamSafetyThreshold).Returns(3);
            SettingsMock.SetupGet(a => a.FixtureCreationConcurrency).Returns(3);
            SettingsMock.SetupGet(a => a.FixtureCheckerFrequency).Returns(10000);
            SettingsMock.SetupGet(a => a.FixturesStateFilePath).Returns(GetType().Assembly.Location);
            SettingsMock.SetupGet(a => a.FixturesStateAutoStoreInterval).Returns(int.MaxValue);

            StateManagerMock = new Mock<IStateManager>();
            MarketRulesManagerMock = new Mock<IMarketRulesManager>();
            StreamHealthCheckValidationMock = new Mock<IStreamHealthCheckValidation>();
            FixtureValidationMock = new Mock<IFixtureValidation>();
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures we don't create more than the pre-defined number of concurrent Stream Listener instances.
        /// It includes
        /// </summary>
        [Test]
        [Category(STREAM_LISTENER_BUILDER_ACTOR_CATEGORY)]
        public void TestConcurrentStreamListenerCreationLimit()
        {
            //
            //Arrange
            //
            SettingsMock.SetupGet(a => a.FixtureCreationConcurrency).Returns(3);
            var resourceFacadeMock = new Mock<IResourceFacade>();
            resourceFacadeMock.SetupGet(o => o.Id).Returns("Fixture1Id");

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
            streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = resourceFacadeMock.Object });
            Task.Delay(TimeSpan.FromMilliseconds(100));
            streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = resourceFacadeMock.Object });
            Task.Delay(TimeSpan.FromMilliseconds(1000));
            streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = resourceFacadeMock.Object });
            Task.Delay(TimeSpan.FromMilliseconds(250));
            streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = resourceFacadeMock.Object });
            Task.Delay(TimeSpan.FromMilliseconds(100));
            streamListenerBuilderActorRef.Tell(new CreateStreamListenerMsg { Resource = resourceFacadeMock.Object });

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
            Task.Delay(TimeSpan.FromMilliseconds(500));
            streamListenerBuilderActorRef.Tell(new StreamListenerCreationCompletedMsg { FixtureId = resourceFacadeMock.Object.Id });
            Task.Delay(TimeSpan.FromMilliseconds(1000));
            streamListenerBuilderActorRef.Tell(new StreamListenerCreationCompletedMsg { FixtureId = resourceFacadeMock.Object.Id });
            Task.Delay(TimeSpan.FromMilliseconds(100));
            streamListenerBuilderActorRef.Tell(new StreamListenerCreationFailedMsg { FixtureId = resourceFacadeMock.Object.Id });

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

        #endregion
    }
}

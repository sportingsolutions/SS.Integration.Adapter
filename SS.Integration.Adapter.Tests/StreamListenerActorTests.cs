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
using Akka.TestKit.NUnit;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamListenerActorTests : TestKit
    {
        private Mock<ISettings> _settingsMock;
        private Mock<IAdapterPlugin> _pluginMock;
        private Mock<IServiceFacade> _serviceMock;
        private Mock<IEventState> _eventStateMock;
        private Mock<IStateManager> _stateManagerMock;

        [SetUp]
        public void SetupTest()
        {
            _pluginMock = new Mock<IAdapterPlugin>();

            _settingsMock = new Mock<ISettings>();
            _settingsMock.Reset();
            _settingsMock.Setup(x => x.ProcessingLockTimeOutInSecs).Returns(10);

            _serviceMock = new Mock<IServiceFacade>();
            _eventStateMock = new Mock<IEventState>();
            _stateManagerMock = new Mock<IStateManager>();

            AdapterActorSystem.Init(_settingsMock.Object, _serviceMock.Object, Sys, false);
        }

        [Test]
        [Category("StreamListenerActor")]
        public void ShouldProcessFirstSnapshotOnInitialization()
        {
            var settingsMock = new Mock<ISettings>();
            var resourceMock = new Mock<IResourceFacade>();
            var snapshotJson = System.Text.Encoding.UTF8.GetString(FixtureSamples.football_inplay_snapshot_1);
            var snapshot = FixtureJsonHelper.GetFromJson(snapshotJson);
            resourceMock.Setup(o => o.Id).Returns(snapshot.Id);
            resourceMock.Setup(o => o.MatchStatus).Returns(MatchStatus.InRunning);
            resourceMock.Setup(o => o.Content).Returns(new Summary { Sequence = 1 });
            resourceMock.Setup(o => o.GetSnapshot())
                .Returns(System.Text.Encoding.UTF8.GetString(FixtureSamples.football_inplay_snapshot_1));
            _stateManagerMock.Setup(o => o.CreateNewMarketRuleManager(It.Is<string>(id => id.Equals(snapshot.Id))))
                .Returns(new Mock<IMarketRulesManager>().Object);

            ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    settingsMock.Object));
            Task.Delay(TimeSpan.FromMilliseconds(500)).Wait();

            _pluginMock.Verify(a =>
                    a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(snapshot.Id)), false),
                Times.Exactly(1));
        }

        [Test]
        [Category("StreamListenerActor")]
        public void ShouldUnSuspendFixtureOnInitializationWhenSnapshotSequenceHasNotChanged()
        {
            var settingsMock = new Mock<ISettings>();
            Mock<IResourceFacade> resourceMock = new Mock<IResourceFacade>();
            var snapshotJson = System.Text.Encoding.UTF8.GetString(FixtureSamples.football_inplay_snapshot_1);
            var snapshot = FixtureJsonHelper.GetFromJson(snapshotJson);
            resourceMock.Setup(o => o.Id).Returns(snapshot.Id);
            resourceMock.Setup(o => o.MatchStatus).Returns(MatchStatus.InRunning);
            resourceMock.Setup(o => o.Content).Returns(new Summary { Sequence = 1 });
            resourceMock.Setup(o => o.GetSnapshot())
                .Returns(System.Text.Encoding.UTF8.GetString(FixtureSamples.football_inplay_snapshot_1));
            _stateManagerMock.Setup(o => o.CreateNewMarketRuleManager(It.Is<string>(id => id.Equals(snapshot.Id))))
                .Returns(new Mock<IMarketRulesManager>().Object);
            Mock<IStateProvider> stateProvider = new Mock<IStateProvider>();
            Mock<ISuspensionManager> suspensionManager = new Mock<ISuspensionManager>();
            _stateManagerMock.SetupGet(o => o.StateProvider)
                .Returns(stateProvider.Object);
            stateProvider.SetupGet(o => o.SuspensionManager)
                .Returns(suspensionManager.Object);
            _eventStateMock.Setup(o => o.GetFixtureState(It.Is<string>(id => id.Equals(snapshot.Id))))
                .Returns(new FixtureState { Id = snapshot.Id, Sequence = 1 });

            ActorOfAsTestActorRef(() =>
                new StreamListenerActor(
                    resourceMock.Object,
                    _pluginMock.Object,
                    _eventStateMock.Object,
                    _stateManagerMock.Object,
                    settingsMock.Object));
            Task.Delay(TimeSpan.FromMilliseconds(500)).Wait();

            _pluginMock.Verify(a =>
                    a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(snapshot.Id)), false),
                Times.Never);
            _pluginMock.Verify(a =>
                    a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(snapshot.Id))),
                Times.Exactly(1));
            suspensionManager.Verify(a =>
                    a.Unsuspend(It.Is<string>(id => id.Equals(snapshot.Id))),
                Times.Exactly(1));
        }

        [Test]
        [Category("StreamListenerActor")]
        public void ShouldStartStreamingWhenResourceNotInSetup()
        {
            var settingsMock = new Mock<ISettings>();
            Mock<IResourceFacade> resourceMock = new Mock<IResourceFacade>();
            var snapshotJson = System.Text.Encoding.UTF8.GetString(FixtureSamples.football_inplay_snapshot_1);
            var snapshot = FixtureJsonHelper.GetFromJson(snapshotJson);
            resourceMock.Setup(o => o.Id).Returns(snapshot.Id);
            resourceMock.Setup(o => o.MatchStatus).Returns(MatchStatus.InRunning);
            resourceMock.Setup(o => o.Content).Returns(new Summary { Sequence = 1 });
            resourceMock.Setup(o => o.GetSnapshot())
                .Returns(System.Text.Encoding.UTF8.GetString(FixtureSamples.football_inplay_snapshot_1));
            _stateManagerMock.Setup(o => o.CreateNewMarketRuleManager(It.Is<string>(id => id.Equals(snapshot.Id))))
                .Returns(new Mock<IMarketRulesManager>().Object);
            Mock<IStateProvider> stateProvider = new Mock<IStateProvider>();
            Mock<ISuspensionManager> suspensionManager = new Mock<ISuspensionManager>();
            _stateManagerMock.SetupGet(o => o.StateProvider)
                .Returns(stateProvider.Object);
            stateProvider.SetupGet(o => o.SuspensionManager)
                .Returns(suspensionManager.Object);
            _eventStateMock.Setup(o => o.GetFixtureState(It.Is<string>(id => id.Equals(snapshot.Id))))
                .Returns(new FixtureState { Id = snapshot.Id, Sequence = 1 });

            var actor = ActorOfAsTestActorRef(() =>
                  new StreamListenerActor(
                      resourceMock.Object,
                      _pluginMock.Object,
                      _eventStateMock.Object,
                      _stateManagerMock.Object,
                      settingsMock.Object));
            Task.Delay(TimeSpan.FromMilliseconds(500)).Wait();

            _pluginMock.Verify(a =>
                    a.ProcessSnapshot(It.Is<Fixture>(f => f.Id.Equals(snapshot.Id)), false),
                Times.Never);
            _pluginMock.Verify(a =>
                    a.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(snapshot.Id))),
                Times.Exactly(1));
            suspensionManager.Verify(a =>
                    a.Unsuspend(It.Is<string>(id => id.Equals(snapshot.Id))),
                Times.Exactly(1));
            resourceMock.Verify(a => a.StartStreaming(), Times.Exactly(1));
            Assert.AreEqual(StreamListenerActor.StreamListenerState.Streaming, actor.UnderlyingActor.State);
        }
    }
}

//Copyright 2014 Spin Services Limited

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
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model.Enums;
using SportingSolutions.Udapi.Sdk.Events;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamListenerTests
    {

        public string _fixtureId = "y9s1fVzAoko805mzTnnTRU_CQy8";

        [Category("Adapter")]
        [Test]
        public void ShouldStartAndStopListening()
        {
            var fixtureSnapshot = new Fixture { Id="TestId", MatchStatus = "30", Sequence = 1 };

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            marketFilterObjectStore.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());

            resource.Setup(r => r.Sport).Returns("Football");
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixtureSnapshot));
            eventState.Setup(e => e.GetCurrentSequence(It.IsAny<string>(), It.IsAny<string>())).Returns(-1);
            

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            listener.Start();

            listener.Stop();

            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), false), Times.Once());
        }

        /*
        [Category("Integration")]
        [Test]
        public void ShouldReceiveDeltaWithSnapshotProcess()
        {
            // We need to review this unit test as it fails on team city
            // while on a local environment is always succed

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch:1, sequence:2);
            var fixtureSnapshot = new Fixture { Id = "y9s1fVzAoko805mzTnnTRU_CQy8", Epoch = 1, MatchStatus = "30", Sequence = 1};

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IDictionary<string, MarketState>>>();
            var are = new AutoResetEvent(false);

            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new Dictionary<string, MarketState>());
            eventState.Setup(e => e.GetCurrentSequence(It.IsAny<string>(), It.IsAny<string>())).Returns(-1);
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamEvent += null, new StreamEventArgs(fixtureDeltaJson));
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            connector.Setup(c => c.ProcessSnapshot(fixtureSnapshot, false));
            connector.Setup(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>())).Callback( () => are.Set());
           
            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence:-1);

            listener.Start();
            are.WaitOne(10000);

            listener.Stop();

            connector.VerifyAll();
            resource.VerifyAll(); 
        }*/

        [Category("Adapter")]
        [Test]
        public void ShouldNotProcessDeltaAsSequenceIsSmaller()
        {
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();
            var fixtureSnapshot = new Fixture { Id = "TestId", Epoch = 0, MatchStatus = "30", Sequence = 11 };

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixtureSnapshot));
            resource.Setup(r => r.Content).Returns(new Summary());
            eventState.Setup(e => e.GetCurrentSequence(It.IsAny<string>(), It.IsAny<string>())).Returns(10);
            marketFilterObjectStore.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());


            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            listener.Start();

            resource.Raise(r => r.StreamEvent += null, new StreamEventArgs(fixtureDeltaJson));

            listener.Stop();

            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), false), Times.Once());
            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.VerifyAll();
        }

        [Test]
        [Category("Adapter")]
        public void ShouldSequenceAndEpochBeValid()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            int matchStatusDelta = 40;
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(matchStatus: matchStatusDelta);
            
            var fixtureSnapshot = new Fixture { Id = "y9s1fVzAoko805mzTnnTRU_CQy8", Epoch = 1, MatchStatus = "30", Sequence = 1 };

            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());
            resource.Setup(r => r.MatchStatus).Returns((MatchStatus)matchStatusDelta);
            resource.Setup(r => r.Sport).Returns("Football");
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixtureSnapshot));
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));
            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();
             
            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            eventState.Verify(es => es.UpdateFixtureState("Football", It.IsAny<string>(), 2, resource.Object.MatchStatus), Times.Once());
        }

        [Test]
        [Category("Adapter")]
        public void ShouldNotProcessStreamUpdateIfSnapshotWasProcessed()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();

            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 20, 0, 30));

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            listener.Start();
            
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            connector.Verify(x=> x.ProcessStreamUpdate(It.IsAny<Fixture>(),It.IsAny<bool>()),Times.Never());

        }

        [Test]
        [Category("Adapter")]
        public void ShouldSequenceBeInvalid()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 5, 0, 30));
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);


            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            resource.Verify(r => r.GetSnapshot(), Times.Once());
            eventState.Verify(es => es.UpdateFixtureState(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),It.IsAny<MatchStatus>()), Times.Once());
        }


        [Test]
        [Category("Adapter")]
        public void CheckStreamHealthWithInvalidSequenceTest()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 5, 0, 30));
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            connector.Setup(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()));

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();
            
            listener.Start();
                        
            //current sequence is 19, 21 is invalid
            listener.CheckStreamHealth(30000, 21);

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            
            resource.Verify(r=> r.GetSnapshot(),Times.Once());
            resource.Verify(r => r.StopStreaming(), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void CheckStreamHealthWithShouldBeSynchronisedWithUpdatesTest()
        {
            
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 18, 0, 30));
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            connector.Setup(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()));

            // the Check Health needs to be done while update is still being processed
            connector.Setup(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()))
                     .Callback(() => listener.CheckStreamHealth(30000, 23));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            listener.Start();

            var nextSequenceFixtureDeltaJson = TestHelper.GetRawStreamMessage();
            resource.Raise(r => r.StreamEvent += null, new StreamEventArgs(nextSequenceFixtureDeltaJson));
            
            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(1));

            resource.Verify(r => r.GetSnapshot(), Times.Once);
            resource.Verify(r => r.StopStreaming(), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void CheckStreamHealthWithValidSequenceTest()
        {

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore.Object);

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            listener.Start();
            
            listener.CheckStreamHealth(30000, 19);

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void ShouldEpochBeValidAsStartTimeHasChanged()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());
            
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();   // Start Time has changed

            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();
        }

        [Test]
        [Category("Adapter")]
        public void ShouldEpochBeInvalidAsCurrentIsGreater()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void ShouldEpochBeInvalidAsFixtureIsDeleted()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();   // Fixture Deleted

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            //listener.IsFixtureEnded.Should().BeTrue();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
        }
        
        [Test]
        [Category("Adapter")]
        public void ShouldEpochBeInvalidAndFixtureEndedAsFixtureIsDeleted()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch:3, sequence:2, matchStatus:50, epochChangeReason:10); // deleted

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            //should be irrelevant
            //listener.IsFixtureEnded.Should().BeTrue();

            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), true), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void ShouldProcessSnapshopWhenReconnecting()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var snapshot = TestHelper.GetSnapshotJson();
            resource.Setup(r => r.GetSnapshot()).Returns(snapshot);
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());

            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object,marketFilterObjectStore.Object);

            listener.ResourceOnStreamConnected(this, EventArgs.Empty);
            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);
            listener.ResourceOnStreamConnected(this, EventArgs.Empty);

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), false), Times.Exactly(2));
            resource.Verify(r => r.GetSnapshot(), Times.Exactly(2));
        }
    }
}

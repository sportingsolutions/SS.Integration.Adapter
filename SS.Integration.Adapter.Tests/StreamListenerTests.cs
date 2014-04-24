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
using System.Threading;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Exceptions;
using SportingSolutions.Udapi.Sdk.Events;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamListenerTests
    {

        public string _fixtureId = "y9s1fVzAoko805mzTnnTRU_CQy8";

        [Category("Integration")]
        [Test]
        public void ShouldStartAndStopListening()
        {
            var fixtureSnapshot = new Fixture { Id="TestId", MatchStatus = "30", Sequence = 1 };

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            resource.Setup(r => r.Sport).Returns("Football");
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            eventState.Setup(e => e.GetCurrentSequence(It.IsAny<string>(), It.IsAny<string>())).Returns(-1);
            

            var listener = new StreamListener("Football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 15);

            listener.Start();

            Thread.Sleep(1000);

            listener.Stop();

            connector.Verify(c => c.ProcessSnapshot(fixtureSnapshot, false), Times.Once());
            connector.VerifyAll();
            resource.VerifyAll();
        }

        [Category("Integration")]
        [Test]
        public void ShouldReceiveDeltaWithSnapshotProcess()
        {
            // We need to review this unit test as it fails on team city
            // while on a local environment is always succed

            /*var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch:1, sequence:2);
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
            resource.VerifyAll(); */
        }

        [Category("Integration")]
        [Test]
        public void ShouldNotProcessDeltaAsEpochHasChanged()
        {
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();
            var fixtureSnapshot = new Fixture { Id = "TestId", Epoch = 0, MatchStatus = "30", Sequence = 11 };

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamEvent += null, new StreamEventArgs(fixtureDeltaJson));
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            eventState.Setup(e => e.GetCurrentSequence(It.IsAny<string>(), It.IsAny<string>())).Returns(10);


            var listener = new StreamListener("Football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.Start();

            Thread.Sleep(1000);

            listener.Stop();

            connector.Verify(c => c.ProcessSnapshot(fixtureSnapshot, false), Times.Once());
            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.VerifyAll();
        }

        [Category("Integration")]
        [Test]
        public void ShouldNotProcessDeltaAsSequenceHasChanged()
        {
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();
            var fixtureSnapshot = new Fixture { Id="TestId", Epoch = 1, MatchStatus = "30", Sequence = 5 };

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamEvent += null, new StreamEventArgs(fixtureDeltaJson));
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.Sport).Returns("Football");
            eventState.Setup(e => e.GetCurrentSequence(It.IsAny<string>(), It.IsAny<string>())).Returns(4);

            var listener = new StreamListener("Football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 5);

            listener.Start();

            Thread.Sleep(1000);

            listener.Stop();

            connector.Verify(c => c.ProcessSnapshot(fixtureSnapshot, false), Times.Once());
            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.VerifyAll();
        }


        [Test]
        public void ShouldSequenceAndEpochBeValid()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            int matchStatusDelta = 40;
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch: 1, sequence: 2, matchStatus: matchStatusDelta);
            
            var fixtureSnapshot = new Fixture { Id = "y9s1fVzAoko805mzTnnTRU_CQy8", Epoch = 1, MatchStatus = "30" };

            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());
            resource.Setup(r => r.MatchStatus).Returns((MatchStatus)matchStatusDelta);
            resource.Setup(r => r.Sport).Returns("Football");

            var listener = new StreamListener("Football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();
             
            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            resource.Verify(r => r.GetSnapshot(), Times.Never());
            eventState.Verify(es => es.UpdateFixtureState("Football", It.IsAny<string>(), 2, resource.Object.MatchStatus), Times.Once());
           
        }

        [Test]
        public void ShouldNotProcessStreamUpdateIfSnapshotWasProcessed()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch: 1, sequence: 19);
            var fixtureSnapshot = new Fixture { Epoch = 1, MatchStatus = "30", Id = "TestFixtureId", Sequence = 20 };

            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object, marketFilterObjectStore.Object, currentSequence: 20);

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 20, 0, 30));

            listener.Start();
            
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            Thread.Sleep(1000);

            connector.Verify(x=> x.ProcessStreamUpdate(It.IsAny<Fixture>(),It.IsAny<bool>()),Times.Never());

        }

        [Test]
        public void ShouldSequenceBeInvalid()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch:1, sequence:19);
            var fixtureSnapshot = new Fixture { Epoch = 1, MatchStatus = "30", Id = "TestFixtureId"};

            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 20);

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1,5,0,30));

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            resource.Verify(r => r.GetSnapshot(), Times.Once());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Once());
            eventState.Verify(es => es.UpdateFixtureState(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),It.IsAny<MatchStatus>()), Times.Once());
        }





        [Test]
        public void CheckStreamHealthWithInvalidSequenceTest()
        {
            var autoSync = new AutoResetEvent(false);
            var snapshotWaitSync = new AutoResetEvent(false); 

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch: 1, sequence: 19);
            var fixtureSnapshot = new Fixture { Epoch = 1, MatchStatus = "30", Id = "TestFixtureId", Sequence = 19 };

            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object, marketFilterObjectStore.Object, currentSequence: 20);

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 5, 0, 30));
            resource.Setup(r => r.StartStreaming()).Callback(() => autoSync.Set());
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);

            connector.Setup(
                x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()))
                     .Callback(() => snapshotWaitSync.Set());
            
            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();
            
            listener.Start();
            
            listener.IsErrored.Should().BeFalse();
            autoSync.WaitOne(10000);
            snapshotWaitSync.WaitOne(10000);

            //current sequence is 19, 21 is invalid
            listener.CheckStreamHealth(30000, 21);

            snapshotWaitSync.WaitOne(10000);

            // even though the sequence is incorrect all you need to do to fix is issue another snapshot
            listener.IsErrored.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(2));
            
            resource.Verify(r=> r.GetSnapshot(),Times.Once());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            eventState.Verify(es => es.UpdateFixtureState(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MatchStatus>()), Times.Exactly(2));
        }

        [Test]
        public void CheckStreamHealthWithShouldBeSynchronisedWithUpdatesTest()
        {
            var autoSync = new AutoResetEvent(false);
            var snapshotWaitSync = new AutoResetEvent(false);

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch: 1, sequence: 19);
            var fixtureSnapshot = new Fixture { Epoch = 1, MatchStatus = "30", Id = _fixtureId, Sequence = 19 };

            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object, marketFilterObjectStore.Object, currentSequence: 20);

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 18, 0, 30));
            resource.Setup(r => r.StartStreaming()).Callback(() => autoSync.Set());
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);

            connector.Setup(
                x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()))
                     .Callback(() => snapshotWaitSync.Set());

            // the Check Health needs to be done while update is still being processed
            connector.Setup(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()))
                     .Callback(() => listener.CheckStreamHealth(30000, 23));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            listener.Start();

            listener.IsErrored.Should().BeFalse();
            autoSync.WaitOne(10000);
            snapshotWaitSync.WaitOne(10000);

            var nextSequenceFixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch: 1, sequence: 20);
            resource.Raise(r => r.StreamEvent += null, new StreamEventArgs(nextSequenceFixtureDeltaJson));
            
            snapshotWaitSync.WaitOne(3000);

            // even though the sequence is incorrect all you need to do to fix is issue another snapshot
            listener.IsErrored.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once());
            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(1));

            resource.Verify(r => r.GetSnapshot(), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            eventState.Verify(es => es.UpdateFixtureState(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MatchStatus>()), Times.Exactly(2));
        }

        [Test]
        public void CheckStreamHealthWithValidSequenceTest()
        {
            var autoSync = new AutoResetEvent(false);

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch: 1, sequence: 19);
            var fixtureSnapshot = new Fixture { Epoch = 1, MatchStatus = "30", Id = "TestFixtureId", Sequence = 19 };

            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object, marketFilterObjectStore.Object, currentSequence: 20);

            //resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 5, 0, 30));
            resource.Setup(r => r.StartStreaming()).Callback(() => autoSync.Set());
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            listener.Start();

            listener.IsErrored.Should().BeFalse();

            autoSync.WaitOne(10000);
            
            listener.CheckStreamHealth(30000, 19);

            listener.IsErrored.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            eventState.Verify(es => es.UpdateFixtureState(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MatchStatus>()), Times.Exactly(1));
        }

        [Test]
        public void ShouldEpochBeValidAsStartTimeHasChanged()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());
            
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch:3, sequence:2, epochChangeReason:30);   // Start Time has changed
            var fixtureSnapshot = new Fixture { Id = "y9s1fVzAoko805mzTnnTRU_CQy8", Epoch = 2, MatchStatus = "30" };

            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            resource.Verify(r => r.GetSnapshot(), Times.Never());

        }

        [Test]
        public void ShouldEpochBeValidAsMatchStatusChanged()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            resource.Setup(r => r.GetSnapshot()).Returns(TestHelper.GetSnapshotJson(3, 2, 40, 40));
            resource.Setup(x => x.Sport).Returns("Football");

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch: 3, sequence: 2, matchStatus: 40, epochChangeReason: 40);   // Match Status has changed
            var fixtureSnapshot = new Fixture { Id = "TestFixtureId", Epoch = 2, MatchStatus = "30",Sequence = 7};

            var listener = new StreamListener("Football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessMatchStatus(It.IsAny<Fixture>()), Times.Once());
            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Once());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            resource.Verify(r => r.GetSnapshot(), Times.Once());
            eventState.Verify(es => es.UpdateFixtureState("Football", It.IsAny<string>(), 2,MatchStatus.InRunning), Times.Once());
        }

        [Test]
        public void ShouldEpochBeInvalidAsCurrentIsGreater()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch:3, sequence:2);
            var fixtureSnapshot = new Fixture { Epoch = 4, MatchStatus = "30" };

            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Once());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            resource.Verify(r => r.GetSnapshot(), Times.Once());
        }

        [Test]
        public void ShouldEpochBeInvalidAsFixtureIsDeleted()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch:3, sequence:2, epochChangeReason:10);   // Fixture Deleted
            var fixtureSnapshot = new Fixture { Id = "y9s1fVzAoko805mzTnnTRU_CQy8", Epoch = 2, MatchStatus = "30" };

            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            //listener.IsFixtureEnded.Should().BeTrue();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Once());
            resource.Verify(r => r.GetSnapshot(), Times.Never());
        }
        
        [Test]
        public void ShouldEpochBeInvalidAndFixtureEndedAsFixtureIsDeleted()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch:3, sequence:2, matchStatus:50, epochChangeReason:10); // deleted
            var fixtureSnapshot = new Fixture { Id = "y9s1fVzAoko805mzTnnTRU_CQy8", Epoch = 2, MatchStatus = "30"};
            
            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            //should be irrelevant
            //listener.IsFixtureEnded.Should().BeTrue();

            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), true), Times.Never());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Once());
            resource.Verify(r => r.GetSnapshot(), Times.Never());
        }

        [Test]
        public void ShouldEpochBeInvalidAndFixtureNotEnded()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());
            

            var snapshot = TestHelper.GetSnapshotJson(epoch: 4, sequence: 3, matchStatus: 30);  // PreMatch
            resource.Setup(r => r.GetSnapshot()).Returns(snapshot);  

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch: 3,sequence: 2);
            var fixtureSnapshot = new Fixture { Id = "TestFixtureId", Epoch = 2, MatchStatus = "30" };

            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), true), Times.Once());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Once());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            resource.Verify(r => r.GetSnapshot(), Times.Once());
        }

        [Test]
        public void ShouldProcessSnapshopWhenReconnecting()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var snapshot = TestHelper.GetSnapshotJson();
            resource.Setup(r => r.GetSnapshot()).Returns(snapshot);

            var fixtureSnapshot = new Fixture { Id = "TestFixtureId", Epoch = 1, MatchStatus = "30" };

            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var listener = new StreamListener("football", resource.Object, fixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.ResourceOnStreamConnected(this, EventArgs.Empty);
            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);
            listener.ResourceOnStreamConnected(this, EventArgs.Empty);

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), false), Times.Once());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Once());
            resource.Verify(r => r.GetSnapshot(), Times.Once());
        }

        [Test]
        public void ShouldFixtureTurnEndedAsMatchIsOver()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(epoch:2, sequence:2, epochChangeReason:40, matchStatus:50);  // epoch reason: match status
            
            var snapshot = TestHelper.GetSnapshotJson(epoch: 2, sequence: 3, matchStatus: 50);
            resource.Setup(r => r.GetSnapshot()).Returns(snapshot);

            var initialFixtureSnapshot = new Fixture { Id = "TestFixtureId", Epoch = 1, MatchStatus = "30" };

            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            var listener = new StreamListener("football", resource.Object, initialFixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeTrue();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), true), Times.Once());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Once());
            resource.Verify(r => r.GetSnapshot(), Times.Once());
        }

        [Category("Integration")]
        [TestCase("10")]
        [TestCase("20")]
        public void ShouldNotStartStreamingIfFixtureIsSetup(string matchStatus)
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            
            var snapshot = new Fixture { Id = "y9s1fVzAoko805mzTnnTRU_CQy8", Epoch = 1, MatchStatus = matchStatus };

            var listener = new StreamListener("football", resource.Object, snapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);
            
            listener.Start();

            Thread.Sleep(1000);

            listener.Stop();

            listener.IsFixtureSetup.Should().BeTrue();

            resource.Verify(r => r.StartStreaming(), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
        }

        [Test]
        public void ShouldSetEventStateWhenFixtureIsIgnored()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            string snapshotId = "y9s1fVzAoko805mzTnnTRU_CQy8";

            var snapshot = new Fixture { Id = snapshotId, Epoch = 1, MatchStatus = "40" };
            connector.Setup(
                x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()))
                     .Throws(new FixtureIgnoredException());

            resource.Setup(r => r.Sport).Returns("football");
            
            resource.Setup(r => r.Id).Returns(snapshotId);

            var listener = new StreamListener("football", resource.Object, snapshot, connector.Object, eventState.Object, marketFilterObjectStore.Object, currentSequence: 1);
            
            listener.Start();

            //listener.Stop();

            listener.IsIgnored.Should().BeTrue();

            resource.Verify(r => r.StartStreaming(), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());

            eventState.Verify(ev => ev.UpdateFixtureState("football", snapshotId,-1,resource.Object.MatchStatus), Times.Once());
        }

        [Test]
        public void ShouldStartStreamingIfFixtureIsNoLongerSetup()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var marketFilterObjectStore = new Mock<IObjectProvider<IMarketStateCollection>>();
            marketFilterObjectStore.Setup(m => m.GetObject(It.IsAny<string>())).Returns(() => new MarketStateCollection());

            resource.Setup(x => x.Id).Returns("TestFixtureId");
            eventState.Setup(x => x.GetFixtureState("TestFixtureId"))
                      .Returns(new FixtureState() { Id = "TestFixtureId", MatchStatus = MatchStatus.Prematch });

            var snapshot = TestHelper.GetSnapshotJson(epoch: 2, sequence: 3, matchStatus: 30);
            resource.Setup(r => r.GetSnapshot()).Returns(snapshot);

            var initialFixtureSnapshot = new Fixture { Id = "TestFixtureId", Epoch = 1, MatchStatus = "10" };

            var listener = new StreamListener("football", resource.Object, initialFixtureSnapshot, connector.Object, eventState.Object,marketFilterObjectStore.Object, currentSequence: 1);

            listener.StartStreaming();

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();
            
            resource.Verify(r => r.StartStreaming(), Times.Once());
            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), false), Times.Once());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Never());
            resource.Verify(r => r.GetSnapshot(), Times.Once());
        }
    }
}

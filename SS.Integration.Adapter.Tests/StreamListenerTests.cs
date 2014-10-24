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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
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

        [SetUp]
        public void SetupSuspensionManager()
        {
            var plugin = new Mock<IAdapterPlugin>();

            var state = new StateManager(new Mock<ISettings>().Object, plugin.Object);
            state.ClearState(_fixtureId);
            state.ClearState(TestHelper.GetFixtureFromResource("rugbydata_snapshot_2").Id);
        }

        [Category("Adapter")]
        [Test]
        public void ShouldStartAndStopListening()
        {
            var fixtureSnapshot = new Fixture { Id = "TestId", MatchStatus = "30", Sequence = 1 };
            
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var settings = new Mock<ISettings>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);

            //marketFilterObjectStore.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());

            resource.Setup(r => r.Sport).Returns("Football");
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixtureSnapshot));
            resource.Setup(r => r.Id).Returns("TestId");
            eventState.Setup(e => e.GetCurrentSequence(It.IsAny<string>(), It.IsAny<string>())).Returns(-1);
            

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

            listener.Start();

            listener.Stop();

            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), false), Times.Once());
        }

        [Category("Adapter")]
        [Test]
        public void ShouldNotProcessDeltaAsSequenceIsSmaller()
        {
            var fixtureSnapshot = new Fixture { Id = "TestId", Epoch = 0, MatchStatus = "30", Sequence = 11 };

            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var settings = new Mock<ISettings>();
            var eventState = new Mock<IEventState>();
            //var marketFilterObjectStore = new Mock<IObjectProvider<IUpdatableMarketStateCollection>>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.StopStreaming()).Raises(r => r.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(r => r.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixtureSnapshot));
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.Id).Returns("TestId");
            eventState.Setup(e => e.GetFixtureState(It.IsAny<string>())).Returns( new FixtureState {Sequence = 10});
            //marketFilterObjectStore.Setup(x => x.GetObject(It.IsAny<string>())).Returns(new MarketStateCollection());


            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

            listener.Start();
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
            var settings = new Mock<ISettings>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);
            int matchStatusDelta = 40;
            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(matchStatus: matchStatusDelta);
            
            var fixtureSnapshot = new Fixture { Id = "y9s1fVzAoko805mzTnnTRU_CQy8", Epoch = 1, MatchStatus = "30", Sequence = 1 };

            resource.Setup(r => r.MatchStatus).Returns((MatchStatus)matchStatusDelta);
            resource.Setup(r => r.Sport).Returns("Football");
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.Id).Returns("y9s1fVzAoko805mzTnnTRU_CQy8");
            resource.Setup(r => r.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixtureSnapshot));
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

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
            var settings = new Mock<ISettings>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();

            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 20, 0, 30));
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

            listener.Start();

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());

        }

        [Test]
        [Category("Adapter")]
        public void ShouldSequenceBeInvalid()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var settings = new Mock<ISettings>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();

            resource.Setup(x => x.GetSnapshot()).Returns(() => TestHelper.GetSnapshotJson(1, 5, 0, 30));
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Id).Returns("TestFixtureId");


            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never());
            resource.Verify(r => r.StopStreaming(), Times.Never());
            resource.Verify(r => r.GetSnapshot(), Times.Once());
            eventState.Verify(es => es.UpdateFixtureState(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MatchStatus>()), Times.Once());
        }

        [Test]
        [Category("Adapter")]
        public void ShouldEpochBeValidAsStartTimeHasChanged()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var settings = new Mock<ISettings>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();   // Start Time has changed

            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

            listener.Start();
            listener.ResourceOnStreamEvent(null, new StreamEventArgs(fixtureDeltaJson));

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

        }

        [Test]
        public void ShouldEpochBeValidAsMatchStatusChanged()
        {
            var resource = new Mock<IResourceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();
            var settings = new Mock<ISettings>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);

            resource.Setup(r => r.GetSnapshot()).Returns(TestHelper.GetSnapshotJson(3, 2, 40));
            resource.Setup(r => r.Sport).Returns("Football");
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();   // Start Time has changed

            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

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
            var settings = new Mock<ISettings>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

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
            var settings = new Mock<ISettings>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage();   // Fixture Deleted

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

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
            var settings = new Mock<ISettings>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);

            var fixtureDeltaJson = TestHelper.GetRawStreamMessage(3, 2, matchStatus: 50, epochChangeReason: 10); // deleted

            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

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
            var settings = new Mock<ISettings>();
            var marketFilterObjectStore = new StateManager(settings.Object, connector.Object);

            var snapshot = TestHelper.GetSnapshotJson();
            resource.Setup(r => r.GetSnapshot()).Returns(snapshot);
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Content).Returns(new Summary());
            resource.Setup(r => r.Id).Returns("TestFixtureId");


            var listener = new StreamListener(resource.Object, connector.Object, eventState.Object, marketFilterObjectStore);

            listener.ResourceOnStreamConnected(this, EventArgs.Empty);
            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);
            listener.ResourceOnStreamConnected(this, EventArgs.Empty);

            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsFixtureSetup.Should().BeFalse();

            connector.Verify(c => c.ProcessSnapshot(It.IsAny<Fixture>(), false), Times.Exactly(2));
            resource.Verify(r => r.GetSnapshot(), Times.Exactly(2));
        }

        /// <summary>
        /// I want to test that if a resource is in
        /// Setup or Ready state, the streaming should NOT start
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldNotStreamOnSetupState()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Setup);
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            // STEP 3: check that is not streaming
            listener.IsStreaming.Should().BeFalse();

            // STEP 4: we do the same but with status Ready

            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Ready);

            listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.IsStreaming.Should().BeFalse();
        }

        /// <summary>
        /// I want to test that if a resource is in
        /// InRunning state, the streaming should start
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldStreamOnInRunningState()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            // STEP 3: check that is streaming
            listener.IsStreaming.Should().BeTrue();

        }

        /// <summary>
        /// I want to test that when a resource
        /// passes from a "InSetup" to a "InRunning" state,
        /// the streaming should start
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldStartStreamingOnMatchStatusChange()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Setup);
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            // STEP 3: check that is NOT streaming
            listener.IsStreaming.Should().BeFalse();

            // STEP 4: put the resource object in "InRunningState" and notify the listener
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            listener.UpdateResourceState(resource.Object);

            // STEP 5: check that the streaming is activated
            listener.IsStreaming.Should().BeTrue();

        }

        /// <summary>
        /// I want to test that in the case that the streamlistener
        /// receives an early disconnect event, the fixture is suspend.
        /// Moreover, I want to test that the adapter is able to
        /// recognize this situation by calling CheckHealthStream
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldSuspendOnEarlyDisconnection()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");

            var provider = new StateManager(settings.Object, connector.Object);            

            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            resource.Setup(r => r.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            // just to be sure that we are streaming
            listener.IsStreaming.Should().BeTrue();

            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);

            // STEP 3: Check the resoults
            listener.IsStreaming.Should().BeFalse();
            listener.IsFixtureEnded.Should().BeFalse();

            connector.Verify(x => x.Suspend(It.Is<string>(y => y == "ABC")), "StreamListener did not suspend the fixture");
        }

        /// <summary>
        /// I want to test that in the case that the streamlistener
        /// receives an a disconnect event due the fact that the fixture
        /// is ended, the stream listener does not generate a suspend request
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldNotSuspendFixtureOnProperDisconnection()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");
            var provider = new StateManager(settings.Object, connector.Object);

            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };
            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString(),
                Epoch = 2,
                LastEpochChangeReason = new[] { (int)EpochChangeReason.MatchStatus }
            };

            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(r => r.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            // just to be sure that we are streaming
            listener.IsStreaming.Should().BeTrue();

            // send the update that contains the match status change
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);

            // STEP 3: Check the resoults
            listener.IsStreaming.Should().BeFalse();
            listener.IsFixtureEnded.Should().BeTrue();

            // suspend is called when the stream listener correctly handles the "IsFixtureEnded" case, 
            // so we need to make sure that is only called once

            connector.Verify(x => x.Suspend(It.Is<string>(y => y == "ABC")));
            
        }

        /// <summary>
        /// I want to test that if a disconnect
        /// event is raise while trying to connect,
        /// then the streamlistener should NOT be in a streaming state
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldNotStreamOnFailedConnectingAttempt()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);


            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            // please note that we raise the disconnect event here
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamDisconnected += null, EventArgs.Empty);

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            // STEP 3: check the result
            listener.IsStreaming.Should().BeFalse();
            listener.IsFixtureEnded.Should().BeFalse();

            resource.Verify(x => x.GetSnapshot(), Times.Never);

            // STEP 4: now we want to check something similar....that 
            // if StartStreaming raises an exception, then the streamlistener
            // is not on a streaing state

            resource.Setup(x => x.StartStreaming()).Throws(new Exception());

            listener.Start();

            // STEP 5: check results
            listener.IsFixtureEnded.Should().BeFalse();
            listener.IsStreaming.Should().BeFalse();

            resource.Verify(x => x.GetSnapshot(), Times.Never);
        }

        /// <summary>
        /// I want to test that I can call
        /// StreamListener.Start() how many times
        /// I want and only one connection is actually
        /// made to the stream server
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldStartStreamingOnlyOnce()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(r => r.Id).Returns("TestFixtureId");

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);


            // STEP 3: raise 100 calls at random (short) delayes to listener.Start()
            for (int i = 0; i < 100; i++)
            {
                Task.Delay(new Random(DateTime.Now.Millisecond).Next(100)).ContinueWith(x => listener.Start());
            }

            // give a change to the thread to start and finish
            Thread.Sleep(1000);

            // STEP 4: verify that only one call to resource.StartStreaming() has been made...
            resource.Verify(x => x.StartStreaming(), Times.Once, "Streaming must start only once!");

            // ... and we are indeed streaming
            listener.IsStreaming.Should().BeTrue();
        }

        /// <summary>
        /// I want to test that when I start
        /// the StreamListener, if the sequence number
        /// coming within the resource is the same
        /// of the last processed snapshot, then
        /// a new snapshot must not be retrieved
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldNotGetASnapshotIfNothingHasChanged()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            FixtureState fixture_state = new FixtureState
            {
                Id = "ABC",
                MatchStatus = MatchStatus.InRunning,
                Sequence = 5
            };

            // please not Sequence = 5
            resource.Setup(x => x.Content).Returns(new Summary { Id = "ABC", Sequence = 5 });
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.Id).Returns("ABC");
            state.Setup(x => x.GetFixtureState("ABC")).Returns(fixture_state);

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            // STEP 3: check the result

            resource.Verify(x => x.GetSnapshot(), Times.Never, "A new snapshot should not have been retrieved!");
            connector.Verify(x => x.UnSuspend(It.IsAny<Fixture>()), Times.Once);
            listener.IsStreaming.Should().BeTrue();
        }

        /// <summary>
        /// I want to test that, if for some unknown reason,
        /// we receive an update that contains a sequence number
        /// smaller than the last processed sequence, then the update
        /// should be discarded
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldReturnOnAlreadyProcessedSequence()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            // Please note Sequence = 3
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 3, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            // ...and Sequence = 2
            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString()
            };

            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));
            resource.Setup(x => x.Id).Returns("ABC");

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never, "Update should be processed");
        }

        /// <summary>
        /// I want to test that, if for some unknown reasons
        /// I miss an update (I get an update with 
        /// sequence > last processed sequence + 1) then I should
        /// get a snapshot
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldGetSnasphotOnInvalidSequence()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");
            var provider = new StateManager(settings.Object, connector.Object);
            
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.Paused).ToString(),
                Epoch = 2,
                LastEpochChangeReason = new[] { (int)EpochChangeReason.MatchStatus }
            };

            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a the epoch change
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));


            // STEP 5: check that ProcessStreamUpdate is called only once (for suspension)!
            connector.Verify(x => x.Suspend(It.Is<string>(y => y == "ABC")));
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(2));
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2), "The streamlistener was supposed to acquire a new snapshot");
        }

        /// <summary>
        /// I want to test that if, for some reasons,
        /// we get a snapshot with a epoch change, then
        /// a snapshot must be retrieved.
        ///
        /// Pay attention that IsStartTimeChanged is considered
        /// an epoch change for which is not necessary
        /// retrieve a new snapshot
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldGetASnapshotOnInvalidEpoch()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            var provider = new StateManager(settings.Object, connector.Object);

            // Please note Sequence = 1
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            // ...and Sequence = 3
            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 3,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString()
            };

            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a wrong sequence number
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));


            // STEP 5: check that ProcessStreamUpdate is called only once (due suspension)!
            connector.Verify(x => x.Suspend(It.Is<string>(y => y == "ABC")));
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(2));
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2), "The streamlistener was supposed to acquire a new snapshot");
        }

        /// <summary>
        /// I want to test that no new snapshots
        /// are retrieved when there is no epoch change
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldNOTGetASnapshotOnValidEpoch()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.InRunning).ToString()
            };

            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a wrong sequence number
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));


            // STEP 5: check that ProcessSnapshot is called only once!
            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once);
            connector.Verify(x => x.Suspend(It.IsAny<string>()), Times.Never);
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once);
            resource.Verify(x => x.GetSnapshot(), Times.Once, "The streamlistener was NOT supposed to acquire a new snapshot");
        }

        /// <summary>
        /// I want to test that if, for some reasons,
        /// we get an empty snapshot
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldGetAnotherSnapshotInCaseEmptySnapshotWasGenerated()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            var provider = new StateManager(settings.Object, connector.Object);

            // Please note Sequence = 1
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.Prematch).ToString() };

            // ...and Sequence = 3
            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 3,
                MatchStatus = ((int)MatchStatus.InRunning).ToString()
            };

            StreamMessage message = new StreamMessage { Content = update };
            
            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.SetupSequence(x => x.GetSnapshot())
                .Returns(FixtureJsonHelper.ToJson(fixture))
                .Returns(FixtureJsonHelper.ToJson(new Fixture()))
                .Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a wrong sequence number
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));


            // STEP 5: check that ProcessStreamUpdate is called only once (due suspension)!
            connector.Verify(x => x.Suspend(It.Is<string>(y => y == "ABC")));
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(2));
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(3), "The streamlistener was supposed to acquire a new snapshot");
        }

        /// <summary>
        /// I want to test whether the StreamListener generates a full 
        /// suspension even if it missed the fixture deletion msg
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void GenerateSuspensionEvenWhenMissedDeletion()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            var firstSnapshot = TestHelper.GetFixtureFromResource("rugbydata_snapshot_2");
            var secondSnapshot = TestHelper.GetFixtureFromResource("rugbydata_snapshot_withRemovedMarkets_5");

            var update = new Fixture
            {
                Id = firstSnapshot.Id,
                //invalid sequence
                Sequence = firstSnapshot.Sequence + 2,
                MatchStatus = firstSnapshot.MatchStatus
            };
            

            StreamMessage message = new StreamMessage { Content =  update };
            
            resource.Setup(x => x.Id).Returns(firstSnapshot.Id);
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.SetupSequence(x => x.GetSnapshot())
                .Returns(FixtureJsonHelper.ToJson(firstSnapshot))
                .Returns(FixtureJsonHelper.ToJson(secondSnapshot));
            
            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a wrong sequence number
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));
            
            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);
            connector.Verify(x => x.Suspend(It.IsAny<string>()), Times.Once);
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(2));
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2), "The streamlistener is supposed to send second");
            
            var marketsRemoved = firstSnapshot.Markets.Where(m => !secondSnapshot.Markets.Exists(m2 => m.Id == m2.Id)).ToList();
            marketsRemoved.Exists(m => m.Id == "_3z0qZjBERuS8kLYiqhuESaDZDM").Should().BeTrue();

            connector.Verify(
                x =>
                    x.ProcessSnapshot(
                        It.Is<Fixture>(
                            f =>
                                f.Sequence == secondSnapshot.Sequence 
                                && marketsRemoved.All(removedMarket => f.Markets.Exists(m=> m.Id == removedMarket.Id && m.IsSuspended))
                                ),
                        It.IsAny<bool>()), Times.Once);
            }

        /// <summary>
        /// I want to test whether the StreamListener generates 
        /// a full suspenssion even if it missed 
        /// the fixture deletion msg
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void GenerateSuspensionOnlyOnceWhenMissedDeletion()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            var firstSnapshot = TestHelper.GetFixtureFromResource("rugbydata_snapshot_2");
            var secondSnapshot = TestHelper.GetFixtureFromResource("rugbydata_snapshot_withRemovedMarkets_5");

            var update = new Fixture
            {
                Id = firstSnapshot.Id,
                //invalid sequence
                Sequence = firstSnapshot.Sequence + 2,
                MatchStatus = firstSnapshot.MatchStatus
            };


            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(x => x.Id).Returns(firstSnapshot.Id);
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            
            // it needs to set up as many snapshots as there will be taken
            resource.SetupSequence(x => x.GetSnapshot())
                .Returns(FixtureJsonHelper.ToJson(firstSnapshot))
                .Returns(FixtureJsonHelper.ToJson(secondSnapshot))
                .Returns(FixtureJsonHelper.ToJson(secondSnapshot));
            
            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);
            
            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a wrong sequence number
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            update.Sequence += 10;
            message.Content = update;
            
            // should cause a snapshot as we missed a sequence
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);
            connector.Verify(x => x.Suspend(It.IsAny<string>()), Times.Exactly(2));
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(3));
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(3), "The streamlistener is supposed to send second");

            var marketsRemoved = firstSnapshot.Markets.Where(m => !secondSnapshot.Markets.Exists(m2 => m.Id == m2.Id)).ToList();
            marketsRemoved.Exists(m => m.Id == "_3z0qZjBERuS8kLYiqhuESaDZDM").Should().BeTrue();

            connector.Verify(
                x =>
                    x.ProcessSnapshot(
                        It.Is<Fixture>(
                            f =>
                                f.Sequence == secondSnapshot.Sequence
                                    && marketsRemoved.Any(removedMarket => f.Markets.Exists(m => m.Id == removedMarket.Id && m.IsSuspended))
                                ),
                        It.IsAny<bool>()), Times.Once);
        }
        
        [Test]
        [Category("StreamListener")]
        public void ShouldStopStreamingIfFixtureIsEnded()
        {
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");
            var provider = new StateManager(settings.Object, connector.Object);

            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString(),
                Epoch = 2,
                LastEpochChangeReason = new[] { (int)EpochChangeReason.MatchStatus }
            };

            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 4: send the update containing a wrong sequence number
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));


            // STEP 5: check that ProcessSnapshot is called only twice (first snapshot and when the fixture is ended, we get another one)!
            connector.Verify(x => x.Suspend(It.Is<string>(y => y == "ABC")));
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Exactly(2));
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2), "The StreamListener was supposed to acquire a new snapshot");
        }

        /// <summary>
        /// I want to test that if an error is
        /// raised while processing a snasphot, then
        /// the StreamListener immediately takes a new 
        /// snapshot for trying to solve the problem.
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldTakeASnapshotOnError()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");

            var provider = new StateManager(settings.Object, connector.Object);

            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            // if we return an emptry string, the StreamListener is supposed to raise an exception
            resource.Setup(x => x.GetSnapshot()).Returns(String.Empty);


            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            // STEP 3: check the result

            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2), "The StreamListener was supposed to acquire 2 snasphots");
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);            
        }

        /// <summary>
        /// I want to test that if the StreamListener
        /// is in an error state and an update arrives,
        /// then, instead of processing the update
        /// a snapshot is retrieved and processed 
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldTakeASnapshotOnUpdateIfItIsInAnErrorState()
        {

            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");
            var provider = new StateManager(settings.Object, connector.Object);

            Fixture fixture = new Fixture { Id = "ABCD", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };
            Fixture update = new Fixture
            {
                Id = "ABCD",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString(),
                Epoch = 2,
                LastEpochChangeReason = new[] { (int)EpochChangeReason.MatchStatus }
            };

            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(x => x.Id).Returns("ABCD");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            // if we return an emptry string, the StreamListener is supposed to raise an exception
            resource.Setup(x => x.GetSnapshot()).Returns(String.Empty);


            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            // STEP 3: check the result

            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2), "The StreamListener was supposed to acquire 2 snasphots");
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);

            // here we are in an error state...

            // STEP 4: 
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            // STEP 5: verify the results

            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never, "The StreamListener was not supposed to process any updates");
            connector.Verify(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Once);
        }

        /// <summary>
        /// Here I want to test that the first time the stream listener
        /// sees a resource (that is in a setup state), 
        /// it needs to retrieve a snapshot in order
        /// to insert the fixture in the downstream system. 
        /// This behaviour must be executed only at the very first time
        /// the streamlistener sees the fixture 
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldTakeASnapshotOnFirstTimeWeSeeAFixture()
        {
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.Setup).ToString() };

            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Setup);

            // FIRST TEST -> if an error is raised, don't reach the error state

            // with returning an empty string we force the stream listener to raise an exception
            resource.Setup(x => x.GetSnapshot()).Returns(string.Empty);

            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            // make sure that the listener has not call StartStreaming
            // but has instead hit the procedure to acquire the first snapshot
            resource.Verify(x => x.StartStreaming(), Times.Never);
            
            // GetSnapshot should immediatelly retry when the first snapshot failed
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2));

            //if the first snapshot fails it will be marked as errored unless a second one 
            listener.IsErrored.Should().BeTrue();

            listener.Stop();

            // SECOND TEST -> make sure we only acquire one snapshot (locking mechanism)

            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // until the procedure of getting and processing the first snapshot is over,
            // no other snapshots should be acquired. To check this, we try to acquire 
            // a new snapshot while we are processing the previous one.
            connector.Setup(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>())).Callback(() =>
            {
                listener.UpdateResourceState(resource.Object);
                resource.Verify(x => x.StartStreaming(), Times.Never);

                // check that GetSnapshot is only called once!
                // 3 = this test + previous one
                resource.Verify(x => x.GetSnapshot(), Times.Exactly(3));
            }
            );


            listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start().Should().BeTrue();
            listener.IsErrored.Should().BeFalse();


            // THIRD TEST -> make sure that if we acquired the first snapshot but
            // while processing we raised an exception, then a new snapshot should
            // be taken

            // until the procedure of getting and processing the first snapshot is over,
            // no other snapshots should be acquired. To check this, we try to acquire 
            // a new snapshot while we are processing the previous one.
            connector.Setup(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>()))
                .Throws(new Exception("While processing the first snapshot, the plugin raised an exception"));
            
            listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            // an exception is raised while we process the first snapshot
            listener.Start().Should().BeFalse();

            listener.IsErrored.Should().BeTrue();

            //3 calls to get snapshot + 2 retries
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(3+2));

            // ...clear the callback so we don't raise any exception
            connector.Setup(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>())).Callback(() => { });

            // ... retry....and it should be fine
            listener.UpdateResourceState(resource.Object);
            
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(6));

            // ... retry again but this time the stream listener should not do anything
            listener.UpdateResourceState(resource.Object);

            resource.Verify(x => x.GetSnapshot(), Times.Exactly(6));
        }


        /// <summary>
        /// I want to test that when the first snapshot errors 
        /// on setup fixture, streamlistener does NOT start streaming 
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldTakeASnapshotOnFirstTimeAndNotStartStreamingIfErrored()
        {
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            Fixture fixture = new Fixture {Id = "ABC", Sequence = 1, MatchStatus = ((int) MatchStatus.Setup).ToString()};

            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Setup);

            // FIRST TEST -> if an error is raised, don't reach the error state

            // with returning an empty string we force the stream listener to raise an exception
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));
            connector.Setup(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>())).Throws<Exception>();

            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start().Should().BeFalse();

            // make sure that the listener has not call StartStreaming
            // but has instead hit the procedure to acquire the first snapshot
            resource.Verify(x => x.StartStreaming(), Times.Never);

            // GetSnapshot should immediatelly retry when the first snapshot failed
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2));

            //if the first snapshot fails it will be marked as errored unless a second one 
            listener.IsErrored.Should().BeTrue();

            resource.Verify(x=> x.StartStreaming(It.IsAny<int>(),It.IsAny<int>()),Times.Never);
        }

        /// <summary>
        /// I want to test that when the first snapshot errors 
        /// on prematch fixture, streamlistener does NOT start streaming 
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void ShouldNotStartStreamingPrematchIfErroredOnSnapshot()
        {
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            IStateManager provider = new StateManager(settings.Object, connector.Object);

            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.Prematch).ToString() };

            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Setup);

            // FIRST TEST -> if an error is raised, don't reach the error state

            // with returning an empty string we force the stream listener to raise an exception
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));
            connector.Setup(x => x.ProcessSnapshot(It.IsAny<Fixture>(), It.IsAny<bool>())).Throws<Exception>();

            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start().Should().BeFalse();

            // make sure that the listener has not call StartStreaming
            // but has instead hit the procedure to acquire the first snapshot
            resource.Verify(x => x.StartStreaming(), Times.Never);

            // GetSnapshot should immediatelly retry when the first snapshot failed
            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2));

            //if the first snapshot fails it will be marked as errored unless a second one 
            listener.IsErrored.Should().BeTrue();
            resource.Verify(x => x.StartStreaming(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        }

        /// <summary>
        /// I want to test that when a fixture
        /// got un-published and re-published
        /// (without any change on the fixture data)
        /// the delta-rule doesn't remove any market
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void MarkFixtureAsUnpublishedTest()
        {
            // STEP 1: prepare the stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            settings.Setup(x => x.DeltaRuleEnabled).Returns(true);
            var provider = new StateManager(settings.Object, connector.Object);

            //var suspended = false;
            //Action<IMarketStateCollection> suspension_strategy = x => {suspended = true;};
            //suspension.RegisterAction(suspension_strategy, SuspensionReason.FIXTURE_DELETED);

            // Please note Sequence = 1
            Fixture fixture = new Fixture { Id = "ABCDE", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString(), Epoch = 1 };

            Market mkt = new Market { Id = "MKT" };

            fixture.Markets.Add(mkt);

            // ...and Sequence = 3
            Fixture update = new Fixture
            {
                Id = "ABCDE",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString(), 
                Epoch = 2, 
                LastEpochChangeReason = new [] {(int)EpochChangeReason.Deleted}
            };

            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(x => x.Id).Returns("ABCDE");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 3: let create at lea
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            connector.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>(y => y.Markets.All(k => k.IsSuspended)), It.IsAny<bool>()));

            update = new Fixture
            {
                Id = "ABCDE",
                Sequence = 3,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString(), 
                Epoch = 2
            };

            mkt = new Market {Id = "MKT"};

            update.Markets.Add(mkt);

            update.Tags.Add("tag", "tag");

            message = new StreamMessage { Content = update };

            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            // STEP 4: if the delta rule was executed, the market would have been removed
            fixture.Markets.FirstOrDefault(x => x.Id == "MKT").Should().NotBeNull();
        }

        [Test]
        [Category("StreamListener")]
        public void MarkMarkedAsForcedSuspended()
        {
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            settings.Setup(x => x.DeltaRuleEnabled).Returns(true);
            var provider = new StateManager(settings.Object, connector.Object);
            provider.SuspensionManager.RegisterAction(provider.SuspensionManager.SuspendAllMarketsStrategy, SuspensionReason.SUSPENSION);

            //var suspended = false;
            //Action<IMarketStateCollection> suspension_strategy = x => {suspended = true;};
            //suspension.RegisterAction(suspension_strategy, SuspensionReason.FIXTURE_DELETED);

            // Please note Sequence = 1
            Fixture fixture = new Fixture { Id = "ABCDE", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString(), Epoch = 1 };

            Market mkt = new Market { Id = "MKT" };

            fixture.Markets.Add(mkt);

            // ...and Sequence = 3
            Fixture update = new Fixture
            {
                Id = "ABCDE",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString(),
                Epoch = 2,
                LastEpochChangeReason = new[] { (int)EpochChangeReason.BaseVariables }
            };

            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(x => x.Id).Returns("ABCDE");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 3: let create at lea
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            var fixtureState = provider.CreateNewMarketRuleManager("ABCDE").CurrentState;
            fixtureState.Markets.All(x => fixtureState[x].IsForcedSuspended).Should().BeFalse();
        }

        /// <summary>
        /// I want to test that when a fixture gets deleted
        /// or it reach the match over status, a call on 
        /// StreamListener.Dispose() doesn't generate 
        /// suspend commands (suspends commands are 
        /// generated while the deleted/matchover updates
        /// are processed)
        /// </summary>
        [Test]
        [Category("StreamListener")]
        public void DontSendSuspensionOnDisposingIfFixtureEndedOrDeletedTest()
        {
            // STEP 1: prepare data

            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();


            var provider = new StateManager(settings.Object, connector.Object);

            bool disposed = false;
            bool matchover = false;
            bool deleted = false;

            Action<IMarketStateCollection> disponsing_strategy = x => {disposed = true;};
            Action<IMarketStateCollection> matchover_strategy = x => { matchover = true; };
            Action<IMarketStateCollection> deleted_strategy = x => { deleted = true; };

            provider.SuspensionManager.RegisterAction(disponsing_strategy, SuspensionReason.FIXTURE_DISPOSING);
            provider.SuspensionManager.RegisterAction(deleted_strategy, SuspensionReason.FIXTURE_DELETED);
            provider.SuspensionManager.RegisterAction(matchover_strategy, SuspensionReason.SUSPENSION);
           

            Fixture fixture = new Fixture { Id = "ABCDE", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString(), Epoch = 1 };

            Market mkt = new Market { Id = "MKT" };

            fixture.Markets.Add(mkt);

            Fixture update = new Fixture
            {
                Id = "ABCDE",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.InRunning).ToString(),
                Epoch = 2,
                LastEpochChangeReason = new[] { (int)EpochChangeReason.Deleted }
            };

            StreamMessage message = new StreamMessage { Content = update };

            resource.Setup(x => x.Id).Returns("ABCDE");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 2: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            disposed.Should().BeFalse();
            matchover.Should().BeFalse();
            deleted.Should().BeFalse();

            // STEP 3: send a delete command and check the result
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            disposed.Should().BeFalse();
            matchover.Should().BeFalse();
            deleted.Should().BeTrue();

            // STEP 4: reset the flags and send the dispose command
            // (no other suspension commands should be raised);
            disposed = false;
            matchover = false;
            deleted = false;

            listener.Dispose();

            deleted.Should().BeFalse();
            disposed.Should().BeFalse();
            matchover.Should().BeFalse();

            // STEP 5: do the same, but for MatchOver 
            disposed = false;
            matchover = false;
            deleted = false;

            listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            disposed.Should().BeFalse();
            matchover.Should().BeFalse();
            deleted.Should().BeFalse();

            update = new Fixture
            {
                Id = "ABCDE",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString(),
                Epoch = 2,
                LastEpochChangeReason = new[] { (int)EpochChangeReason.MatchStatus }
            };

            message = new StreamMessage { Content = update };

            // STEP 3: send a delete command and check the result
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            disposed.Should().BeFalse();
            matchover.Should().BeTrue();
            deleted.Should().BeFalse();

            // STEP 4: reset the flags and send the dispose command
            // (no other suspension commands should be raised);
            disposed = false;
            matchover = false;
            deleted = false;

            listener.Dispose();

            deleted.Should().BeFalse();
            disposed.Should().BeFalse();
            matchover.Should().BeFalse();
        }
    }
}

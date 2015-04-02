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
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Events;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    class SuspensionTests
    {

        /// <summary>
        /// I want to test that all the default
        /// suspension strategies correctly executes
        /// when we pass in a IMarketStateCollection
        /// </summary>
        [Test]
        [Category("Suspension")]
        public void SuspensionManagerStrategiesTest()
        {
            var stateProvider = new Mock<IStateProvider>();
            var plugin = new Mock<IAdapterPlugin>();
            var suspensionManager = new SuspensionManager(stateProvider.Object, plugin.Object);

            var state = new MarketStateCollection("FXT-ID");

            // STEP 1: prepare the fixture 
            // 1) fixture is in running
            // 2) with 2 in play markets
            // 3) 1 not in-play market
            // 4) 3 markets with an unknown state
            Fixture fixture = new Fixture
            {
                Id = "FXT-ID",
                MatchStatus = MatchStatus.InRunning.ToString(),
                Sequence = 2
            };

            var mkt1 = new Market { Id = "MKT-1" };
            mkt1.Selections.Add(new Selection { Id = "SELN", Status = SelectionStatus.Active });

            var mkt2 = new Market { Id = "MKT-2" };
            mkt2.Selections.Add(new Selection { Id = "SELN", Status = SelectionStatus.Active });

            var mkt3 = new Market { Id = "MKT-3" };
            mkt3.Selections.Add(new Selection { Id = "SELN", Status = SelectionStatus.Pending });

            var mkt4 = new Market { Id = "MKT-4" };
            mkt4.Selections.Add(new Selection { Id = "SELN", Status = SelectionStatus.Active });
            mkt4.AddOrUpdateTagValue("traded_in_play", "false");

            var mkt5 = new Market { Id = "MKT-5" };
            mkt5.Selections.Add(new Selection { Id = "SELN", Status = SelectionStatus.Active });
            mkt5.AddOrUpdateTagValue("traded_in_play", "true");

            var mkt6 = new Market { Id = "MKT-6" };
            mkt6.AddOrUpdateTagValue("traded_in_play", "true");

            fixture.Markets.Add(mkt1);
            fixture.Markets.Add(mkt2);
            fixture.Markets.Add(mkt3);
            fixture.Markets.Add(mkt4);
            fixture.Markets.Add(mkt5);
            fixture.Markets.Add(mkt6);
            state.Update(fixture, true);
            
            state.CommitChanges();
            
            // STEP 2: test the suspension strategies
            suspensionManager.SuspendAllMarketsStrategy(state);

            plugin.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>
                (
                    y => y.Markets.Count == 6 &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-1") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-2") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-3") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-4") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-5") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-6") != null
                ), It.IsAny<bool>()));


            suspensionManager.SuspendFixtureIfInPlayStrategy(state);

            plugin.Verify(x => x.Suspend(It.Is<string>(y => y == "FXT-ID")));

            suspensionManager.SuspendFixtureStrategy(state);

            plugin.Verify(x => x.Suspend(It.Is<string>(y => y == "FXT-ID")));

            suspensionManager.SuspendInPlayMarketsStrategy(state);

            // The SuspensionManager takes a conservative approach.
            // If the traded_in_play tag is not present, it assumes
            // that the market is a in-play market
            plugin.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>
                (
                    y => y.Markets.Count == 3 &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-1") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-2") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-3") == null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-4") == null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-5") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-6") == null
                ), It.IsAny<bool>()));


            // STEP 3: change the fixture
            // 1) change the fixture's match status
            // 2) remove a mkt
            // 3) add a new mkt

            fixture.MatchStatus = MatchStatus.MatchOver.ToString();

            fixture.Markets.Remove(fixture.Markets.FirstOrDefault(x => x.Id == "MKT-5"));

            var mkt7 = new Market { Id = "MKT-7" };
            mkt7.Selections.Add(new Selection { Id = "SELN", Status = SelectionStatus.Active });
            mkt7.AddOrUpdateTagValue("traded_in_play", "true");

            fixture.Markets.Add(mkt7);

            state.Update(fixture, true);
            state.CommitChanges();
            
            // STEP 4: test the suspension strategies again
            suspensionManager.SuspendAllMarketsStrategy(state);

            // note that we must have 7 markets now because the 
            // SuspensionManager looks at the MarketState, not
            // at the fixture
            plugin.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>
                (
                    y => y.Markets.Count == 7 &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-1") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-2") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-3") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-4") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-5") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-6") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-7") != null 
                ), It.IsAny<bool>()));


            suspensionManager.SuspendFixtureIfInPlayStrategy(state);

            // The fixture is "MatchOver", so the Suspend() method should not have been called
            // Times.Exactly(2) because it has been called in the previous step...unfortunately
            // there seems to be no way to reset the verifications
            plugin.Verify(x => x.Suspend(It.Is<string>(y => y == "FXT-ID")), Times.Exactly(2));

            suspensionManager.SuspendFixtureStrategy(state);

            plugin.Verify(x => x.Suspend(It.Is<string>(y => y == "FXT-ID")), Times.Exactly(3));

            suspensionManager.SuspendInPlayMarketsStrategy(state);

            plugin.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>
                (
                    y => y.Markets.Count == 4 &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-1") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-2") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-3") == null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-4") == null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-5") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-6") == null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-7") != null
                ), It.IsAny<bool>()));

        }

        /// <summary>
        /// I want to test whether the StreamListener
        /// upon a disconnection event, suspends 
        /// the fixture
        /// </summary>
        [Test]
        [Category("Suspension")]
        public void SuspendFixtureOnDisconnectTest()
        {
            // STEP 1: prepare stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            var provider = new StateManager(settings.Object, connector.Object);

            provider.SuspensionManager.RegisterAction(provider.SuspensionManager.SuspendInPlayMarketsStrategy, SuspensionReason.DISCONNECT_EVENT);

            // STEP 3: prepare the fixture data
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };
            fixture.Tags.Add("Sport", "Football"); // add at least one tags, so the MarketsRulesManager recognize it as a full-snapshot

            var mkt = new Market("MKT-1");
            mkt.Selections.Add(new Selection {Id = "SELN-1", Status = SelectionStatus.Active});
            fixture.Markets.Add(mkt);
           
            mkt = new Market("MKT-2");
            mkt.AddOrUpdateTagValue("traded_in_play", "false");
            fixture.Markets.Add(mkt);

            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 4: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider, settings.Object);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();            
            

            // STEP 5: send the disconnect event
            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);


            // STEP 6: check the result, note tha MKT-2 is not in-play
            connector.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>(
                y => y.Id == "ABC" &&
                     y.Markets.Count == 1 &&
                     y.Markets.FirstOrDefault(z => z.Id == "MKT-1") != null &&
                     y.Markets.FirstOrDefault(z => z.Id == "MKT-2") == null),
                It.IsAny<bool>()));

        }

        /// <summary>
        /// I want to test that when the StreamListener
        /// receives a disconnect event, and the fixture
        /// is ended, it does not suspend the fixture
        /// </summary>
        [Test]
        [Category("Suspension")]
        public void DoNotSuspendFixtureOnDisconnectIfMatchOverTest()
        {
            // STEP 1: prepare stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            var provider = new StateManager(settings.Object, connector.Object);

            provider.SuspensionManager.RegisterAction(provider.SuspensionManager.SuspendInPlayMarketsStrategy, SuspensionReason.DISCONNECT_EVENT);

            // STEP 3: prepare the fixture data
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };
            fixture.Tags.Add("Sport", "Football"); // add at least one tags, so the MarketsRulesManager recognize it as a full-snapshot

            fixture.Markets.Add(new Market("MKT-1"));
            var mkt = new Market("MKT-2");
            mkt.AddOrUpdateTagValue("traded_in_play", "false");
            fixture.Markets.Add(mkt);

            // ...and MatchStatus=MatchOver
            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.MatchOver).ToString(),
                Epoch = 2,
                LastEpochChangeReason = new[] {(int)EpochChangeReason.MatchStatus}
            };

            StreamMessage message = new StreamMessage { Content = update };


            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 4: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider, settings.Object);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 5: send the update
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));

            // check that the fixture got suspended
            connector.Verify(x => x.Suspend(It.Is<string>(y => y == "ABC")));

            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);

            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);

            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);
        }

        /// <summary>
        /// I want to test that when the StreamListener
        /// receives a disconnect event, and the fixture
        /// is ended, it does not suspend
        /// the fixture
        /// </summary>
        [Test]
        [Category("Suspension")]
        public void DoNotSuspendFixtureOnDisconeectIfDeletedTest()
        {
            // STEP 1: prepare stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            var provider = new StateManager(settings.Object, connector.Object);


            // STEP 3: prepare the fixture data
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };
            fixture.Tags.Add("Sport", "Football"); // add at least one tags, so the MarketsRulesManager recognize it as a full-snapshot

            fixture.Markets.Add(new Market("MKT-1"));
            var mkt = new Market("MKT-2");
            mkt.AddOrUpdateTagValue("traded_in_play", "false");
            fixture.Markets.Add(mkt);

            // ...and MatchStatus=MatchOver
            Fixture update = new Fixture
            {
                Id = "ABC",
                Sequence = 2,
                MatchStatus = ((int)MatchStatus.InRunning).ToString(),
                Epoch = 2,
                LastEpochChangeReason = new[] {(int)EpochChangeReason.Deleted}
            };

            StreamMessage message = new StreamMessage { Content = update };


            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.StopStreaming()).Raises(x => x.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 4: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider, settings.Object);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            // STEP 5: send the update
            listener.ResourceOnStreamEvent(this, new StreamEventArgs(JsonConvert.SerializeObject(message)));


            // STEP 6: check the result, note tha MKT-2 is not in-play
            connector.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>(y => y.Id == "ABC" && y.Markets.Count == 0), It.IsAny<bool>()));

        }


        /// <summary>
        /// I want to test that when a StreamListener
        /// object is disposed, a proper suspension 
        /// request is sent
        /// </summary>
        [Test]
        [Category("Suspension")]
        public void SuspendFixtureOnStreamListenerDisposeTest()
        {
            // STEP 1: prepare stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            var provider = new StateManager(settings.Object, connector.Object);

            provider.SuspensionManager.RegisterAction(provider.SuspensionManager.SuspendInPlayMarketsStrategy, SuspensionReason.DISCONNECT_EVENT);

            // STEP 3: prepare the fixture data
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };
            fixture.Tags.Add("Sport", "Football"); // add at least one tags, so the MarketsRulesManager recognize it as a full-snapshot

            fixture.Markets.Add(new Market("MKT-1"));
            var mkt = new Market("MKT-2");
            mkt.AddOrUpdateTagValue("traded_in_play", "false");
            fixture.Markets.Add(mkt);


            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.StopStreaming()).Raises(x => x.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 4: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider, settings.Object);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();

            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);

            listener.Dispose();

            // STEP 5: check the result, note tha MKT-2 is not in-play
            connector.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>(
                y => y.Id == "ABC" &&
                     y.Markets.Count == 1 &&
                     y.Markets.FirstOrDefault(z => z.Id == "MKT-1") != null),
                It.IsAny<bool>()), Times.Once);

        }







        /// <summary>
        /// I want to test that when a StreamListener
        /// object is disposed, a proper suspension 
        /// request is sent
        /// </summary>
        [Test]
        [Category("Suspension")]
        public void UnsuspendFixtureAndMarketsTest()
        {
            // STEP 1: prepare stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            var provider = new StateManager(settings.Object, connector.Object);

            provider.SuspensionManager.RegisterAction(provider.SuspensionManager.SuspendInPlayMarketsStrategy, SuspensionReason.DISCONNECT_EVENT);

            // STEP 2: prepare the fixture data
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };
            fixture.Tags.Add("Sport", "Football"); // add at least one tags, so the MarketsRulesManager recognize it as a full-snapshot

            var mkt1 = new Market { Id = "MKT-1" };
            mkt1.Selections.Add(new Selection { Id = "SELN", Status = SelectionStatus.Active, Tradable = true });
            fixture.Markets.Add(mkt1);

            var mkt = new Market("MKT-2");
            mkt.AddOrUpdateTagValue("traded_in_play", "false");
            fixture.Markets.Add(mkt);
            
            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary() { Sequence = 1 } );
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.StopStreaming()).Raises(x => x.StreamDisconnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 4: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider, settings.Object);

            listener.Start();

            listener.IsStreaming.Should().BeTrue();
            
            connector.Verify(x => x.ProcessStreamUpdate(It.IsAny<Fixture>(), It.IsAny<bool>()), Times.Never);

            listener.Dispose();

            // STEP 5: check the result, note tha MKT-2 is not in-play
            connector.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>(
                y => y.Id == "ABC" &&
                     y.Markets.Count == 1 &&
                     y.Markets.FirstOrDefault(z => z.Id == "MKT-1") != null),
                It.IsAny<bool>()), Times.Once);

            //recreate listener
            //must have state setup otherwise it will process snapshot not unsuspend
            state.Setup(x => x.GetFixtureState(It.Is<string>(fId => fId == fixture.Id)))
                .Returns(new FixtureState()
                {
                    Id = fixture.Id,
                    MatchStatus = MatchStatus.InRunning,
                    Sequence = fixture.Sequence
                });

            listener = new StreamListener(resource.Object, connector.Object, state.Object, provider, settings.Object);
            listener.Start();

            // Unsuspend should be called on reconnect causing markets to unsuspend:
            connector.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>(
                y => y.Id == "ABC" &&
                     y.Markets.Count == 1 &&
                     y.Markets.FirstOrDefault(z => z.Id == "MKT-1").IsSuspended == false),
                It.IsAny<bool>()), Times.Once);

        }
    }
}

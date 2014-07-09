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
using NUnit.Framework;
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

            fixture.Markets.Add(new Market { Id = "MKT-1" });
            fixture.Markets.Add(new Market { Id = "MKT-2" });
            fixture.Markets.Add(new Market { Id = "MKT-3" });

            Market mkt = new Market { Id = "MKT-4" };
            mkt.AddOrUpdateTagValue("traded_in_play", "false");
            fixture.Markets.Add(mkt);
            
            mkt = new Market { Id = "MKT-5" };
            mkt.AddOrUpdateTagValue("traded_in_play", "true");
            fixture.Markets.Add(mkt);

            mkt = new Market { Id = "MKT-6" };
            mkt.AddOrUpdateTagValue("traded_in_play", "true");
            fixture.Markets.Add(mkt);

            state.Update(fixture, true);
            
            
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
                    y => y.Markets.Count == 5 &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-1") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-2") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-3") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-4") == null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-5") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-6") != null
                ), It.IsAny<bool>()));


            // STEP 3: change the fixture
            // 1) change the fixture's match status
            // 2) remove a mkt
            // 3) add a new mkt

            fixture.MatchStatus = MatchStatus.MatchOver.ToString();

            fixture.Markets.Remove(fixture.Markets.FirstOrDefault(x => x.Id == "MKT-5"));

            mkt = new Market { Id = "MKT-7" };
            mkt.AddOrUpdateTagValue("traded_in_play", "true");

            fixture.Markets.Add(mkt);

            state.Update(fixture, true);
            
            
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
                    y => y.Markets.Count == 6 &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-1") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-2") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-3") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-4") == null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-5") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-6") != null &&
                         y.Markets.FirstOrDefault(z => z.Id == "MKT-7") != null
                ), It.IsAny<bool>()));

        }


        [Test]
        [Category("Suspension")]
        public void SuspendFixtureOnDisconnectTest()
        {
            // STEP 1: prepare stub data
            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
            Mock<IEventState> state = new Mock<IEventState>();
            Mock<ISettings> settings = new Mock<ISettings>();
            var provider = new StateManager(settings.Object);


            // STEP 2: make sure that we use the SuspendInPlayMarketsStrategy
            // if a suspension request with reason = disconnect arrives
            SuspensionManager suspension = new SuspensionManager(provider, connector.Object);

            suspension.RegisterAction(suspension.SuspendInPlayMarketsStrategy, SuspensionReason.DISCONNECT_EVENT);

            // STEP 3: prepare the fixture data
            Fixture fixture = new Fixture { Id = "ABC", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

            fixture.Markets.Add(new Market("MKT-1"));
            var mkt = new Market("MKT-2");
            mkt.AddOrUpdateTagValue("traded_in_play", "false");
            fixture.Markets.Add(mkt);

            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary());
            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            // STEP 4: start the listener
            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider);

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
    }
}

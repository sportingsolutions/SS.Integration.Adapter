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

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.MarketRules;
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class MarketsFilterTests
    {
        private Fixture _snapshot;

        private Mock<Market> _market1;

        private Mock<Market> _market2;

        private Mock<Market> _market3;

        private IStoredObjectProvider _objectProvider;

        private IUpdatableMarketStateCollection _marketStorage;
            
        [SetUp]
        public void SetUp()
        {
            _marketStorage = null;
            _objectProvider = null;

            SetUpSnapshotAndMarkets();

            var objectProviderMock = new Mock<IStoredObjectProvider>();

            // if there's a better way of assigning parameter let me know
            objectProviderMock.Setup(x => x.SetObject(It.IsAny<string>(), It.IsAny<IUpdatableMarketStateCollection>()))
                              .Callback<string, IUpdatableMarketStateCollection>((s, newState) => _marketStorage = newState);

            objectProviderMock.Setup(x => x.GetObject(It.IsAny<string>())).Returns(() => _marketStorage);

            _objectProvider = objectProviderMock.Object;
        }

        private void SetUpSnapshotAndMarkets(MatchStatus status = MatchStatus.InRunning)
        {
            _snapshot = new Fixture {Id = "123", MatchStatus = ((int) status).ToString()};
            _snapshot.Tags.Add("Sport", "TestFootball");
            _market1 = new Mock<Market>();
            _market2 = new Mock<Market>();
            _market3 = new Mock<Market>();

            // Initial Active Market
            _market1.SetupAllProperties();
            _market1.Setup(m => m.Selections).Returns(GetSelections(true, false));
            _market1.Object.Id = "One";
            _market1.Object.AddOrUpdateTagValue("name", "One");

            // Initial Inactive (pending) Market

            _market2.SetupAllProperties();
            _market2.Setup(m => m.Selections).Returns(GetSelections(false, false));
            _market2.Object.Id = "Two";
            _market2.Object.AddOrUpdateTagValue("name", "Two");


            // Initial Active Market
            _market3.SetupAllProperties();
            _market3.Setup(m => m.Selections).Returns(GetSelections(true, false));
            _market3.Object.Id = "Three";
            _market3.Object.AddOrUpdateTagValue("name", "Three");

            _snapshot.Markets.Add(_market1.Object);
            _snapshot.Markets.Add(_market2.Object);
            _snapshot.Markets.Add(_market3.Object);
        }

        [Test]
        public void ShouldRemoveInactiveMarketsFromSnapshot()
        {
            // 1) Filter is created with initial snapshot
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };

            var filteredMarkets = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);

            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();


            // 2) AllMarkets are already created and first update arrives
            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            _snapshot.Markets.Should().Contain(_market1.Object);
            _snapshot.Markets.Should().NotContain(_market2.Object);
            _snapshot.Markets.Should().Contain(_market3.Object);
        }

        [Test]
        public void ShouldRemoveInactiveMarketsFromMultipleUpdates()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };
            // 1) Filter is created with initial snapshot
            var filteredMarkets = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);


            // 2) AllMarkets are already created and first update arrives
            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            _snapshot.Markets.Should().Contain(_market1.Object);
            _snapshot.Markets.Should().Contain(_market2.Object);  // not sent as market2 is still inactive
            _snapshot.Markets.Should().Contain(_market3.Object);

            // 3) New update arrives but market1 is inactive this time
            SetUpSnapshotAndMarkets();
            _market1.Setup(s => s.Selections).Returns(GetSelections(false, false));

            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            _snapshot.Markets.Should().Contain(m => AreIdsEqual(m, _market1));     // market1 will update with its new status of pending
            _snapshot.Markets.Should().NotContain(m => AreIdsEqual(m, _market2));  // market2 is still inactive
            _snapshot.Markets.Should().Contain(m => AreIdsEqual(m, _market3));     // no changes for active market3

            // 4) New update arrives with no changes (market1 is still inactive)
            SetUpSnapshotAndMarkets();
            _market1.Setup(s => s.Selections).Returns(GetSelections(false, false));

            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            _snapshot.Markets.Should().NotContain(m => AreIdsEqual(m, _market1));  // market1 will not update as it was inactive before and still inactive
            _snapshot.Markets.Should().NotContain(m => AreIdsEqual(m, _market2));  // no changes for inactive market2
            _snapshot.Markets.Should().Contain(m => AreIdsEqual(m, _market3));     // no changes for active market3
        }

        private bool AreIdsEqual(Market market, Mock<Market> mockMarket)
        {
            return market.Id == mockMarket.Object.Id;
        }

        [Test]
        public void ShouldNotRemoveInactiveMarketsWhenNameChanges()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };
            // 1) Filter is created with initial snapshot
            var filteredMarkets = new MarketRulesManager(_snapshot.Id,_objectProvider, rules);

            // 2) AllMarkets are already created and first update arrives with a change in name for inactive market2
            _market2.Object.AddOrUpdateTagValue("name", "Market Two with new name");

            filteredMarkets.ApplyRules(_snapshot);

            _snapshot.Markets.Should().Contain(_market1.Object);   // market1 updates as is active
            _snapshot.Markets.Should().Contain(_market2.Object);   // market2 updates as its name changed even though is still inactive
            _snapshot.Markets.Should().Contain(_market3.Object);   // market3 updates as is active
        }

        [Test]
        public void ShouldNotRemoveInactiveMarketsWhenStatusChanges()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };
            // 1) Filter is created with initial snapshot
            var filteredMarkets = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);

            _market2.Setup(x => x.Selections).Returns(GetSelections(true, true));
            filteredMarkets.ApplyRules(_snapshot);


            _snapshot.Markets.Should().Contain(_market1.Object);   // market1 updates as is active
            _snapshot.Markets.Should().Contain(_market2.Object);   // market2 updates as its name changed even though is still inactive
            _snapshot.Markets.Should().Contain(_market3.Object);   // market3 updates as is active
        }

        [Test]
        public void ShouldNotRemoveInactiveMarketsWhenGetsActive()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };
            // 1) Filter is created with initial snapshot
            var filteredMarkets = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);

            _market2.Setup(x => x.Selections).Returns(GetSelections(true, false));    

            filteredMarkets.ApplyRules(_snapshot);
            
            _snapshot.Markets.Should().Contain(_market1.Object);   // market1 updates as is active
            _snapshot.Markets.Should().Contain(_market2.Object);   // market2 updates as is now active
            _snapshot.Markets.Should().Contain(_market3.Object);   // market3 updates as is active
        }

        [Test]
        public void MarketNameChangedShouldNotRemove()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };
            // 1) Filter is created with initial snapshot
            var filteredMarkets = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);
            _snapshot.MatchStatus = "40";
            _market2.Object.AddOrUpdateTagValue("name", "newName");
            //_market2.Setup(x => x.Name).Returns("NewName");
            filteredMarkets.ApplyRules(_snapshot);

            _snapshot.Markets.Should().Contain(_market2.Object);
        }

        [Test]
        public void AutoVoidForUnsettledMarkets()
        {

            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };
            // 1) Filter is created with initial snapshot
            var marketsFilter = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);

            marketsFilter.ApplyRules(_snapshot);

            marketsFilter.CommitChanges();

            _snapshot.MatchStatus = "50";
            _snapshot.Markets.RemoveAll(m => m.Id == _market2.Object.Id);

            _market1.Setup(x => x.Selections).Returns(GetSettledSelections());
            _market3.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Void, false));


            marketsFilter.ApplyRules(_snapshot);

            marketsFilter.CommitChanges();

            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeTrue();
            var marketVoided = _snapshot.Markets.First(m => m.Id == _market2.Object.Id);
            marketVoided.Should().NotBeNull();
            marketVoided.Selections.All(s => s.Status == "3").Should().BeTrue();
        }

        [Test]
        public void AutoVoidForUnsettledMarketsShouldNotAffectUnsettledFixtures()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };
            // 1) Filter is created with initial snapshot
            var marketsFilter = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);
            _snapshot.MatchStatus = "10";

            marketsFilter.ApplyRules(_snapshot);

            _snapshot.Markets.RemoveAll(m => m.Id == _market2.Object.Id);

            _market1.Setup(x => x.Selections).Returns(GetSettledSelections());
            _market3.Setup(x => x.Selections).Returns(GetSelections("3", false));

            marketsFilter.ApplyRules(_snapshot);

            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeFalse();


            marketsFilter.ApplyRules(_snapshot);

            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeFalse();
        }

        [Test]
        public void AutoVoidingIgnoresPreviouslyRemovedSettledMarkets()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };
            // 1) Filter is created with initial snapshot
            var marketsFilter = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);

            marketsFilter.ApplyRules(_snapshot);
            marketsFilter.CommitChanges();

            _snapshot.MatchStatus = "50";
            _market1.Setup(x => x.Selections).Returns(GetSettledSelections());
            
            marketsFilter.ApplyRules(_snapshot);
            marketsFilter.CommitChanges();

            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeTrue();
            _snapshot.Markets.First(m => m.Id == _market2.Object.Id).IsResulted.Should().BeTrue();
            _snapshot.Markets.RemoveAll(m => m.Id == _market2.Object.Id);
            
            _market3.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Void, false));

            marketsFilter.ApplyRules(_snapshot);
            marketsFilter.CommitChanges();


            marketsFilter.ApplyRules(_snapshot);

            _snapshot.Markets.Exists(m => m.Id == _market1.Object.Id).Should().BeFalse();
        }

        [Test]
        public void AutoVoidingEverythingThatWasntVoided()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };

            // 1) Filter is created with initial snapshot
            var marketsFilter = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);

            _market1.Setup(x => x.Selections).Returns(GetSettledSelections());
            
            marketsFilter.ApplyRules(_snapshot);
            marketsFilter.CommitChanges();

            _snapshot.Markets.Exists(m => m.Id == _market1.Object.Id).Should().BeTrue();
            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeTrue();
            _snapshot.Markets.Exists(m => m.Id == _market3.Object.Id).Should().BeTrue();


            _snapshot.MatchStatus = "50";
            _market3.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));


            marketsFilter.ApplyRules(_snapshot);
            marketsFilter.CommitChanges();
            
            _snapshot.Markets.Exists(m => m.Id == _market1.Object.Id).Should().BeFalse();
            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeTrue();
            _snapshot.Markets.Exists(m => m.Id == _market3.Object.Id).Should().BeTrue();

            _snapshot.Markets.First(m => m.Id == _market2.Object.Id).IsResulted.Should().BeTrue();
            
            //market 3 was previously active so it shouldn't be voided
            _snapshot.Markets.First(m => m.Id == _market3.Object.Id).IsResulted.Should().BeFalse();
        }

        [Test]
        public void SuspendAllMarketsTest()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };
            
            var settings = new Mock<ISettings>();
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");

            var plugin = new Mock<IAdapterPlugin>();
            var stateManager = new StateManager(settings.Object, plugin.Object);

            // 1) Filter is created with initial snapshot
            var marketsFilter = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);
            _market1.Setup(x => x.Selections).Returns(GetSelections(true, false));
            _market2.Setup(x => x.Selections).Returns(GetSelections(true, false));
            _market3.Setup(x => x.Selections).Returns(GetSelections(true, false));

            marketsFilter.ApplyRules(_snapshot);
            //marketsFilter.CommitChanges();

            _snapshot.Markets.Count.Should().Be(3);
            foreach (var mkt in _snapshot.Markets)
            {
                mkt.Selections.All(y => y.Tradable.HasValue && y.Tradable.Value).Should().BeTrue();
            }

            stateManager.SuspensionManager.SuspendAllMarketsStrategy(marketsFilter.CurrentState);

            plugin.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>(
                    y => y.Markets.Count == 3 &&
                        // check selections tradability on each market
                    !y.Markets.Any(z => z.Selections.Any(k => !k.Tradable.HasValue || k.Tradable.Value))),
                    It.IsAny<bool>()));
        }

        [Test]
        public void SuspendAllMarketsWithEmptySnapshotTest()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };

            var settings = new Mock<ISettings>();
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");

            var plugin = new Mock<IAdapterPlugin>();
            var stateManager = new StateManager(settings.Object, plugin.Object);

            // 1) Filter is created with initial snapshot
            var marketsFilter = new MarketRulesManager(_snapshot.Id, stateManager, rules);
            _market1.Setup(x => x.Selections).Returns(GetSelections(true, false));
            _market2.Setup(x => x.Selections).Returns(GetSelections(true, false));
            _market3.Setup(x => x.Selections).Returns(GetSelections(true, false));

            marketsFilter.ApplyRules(_snapshot);
            marketsFilter.CommitChanges();

            var mkt_count = _snapshot.Markets.Count;
            _snapshot.Markets.Clear();
            _snapshot.MatchStatus = ((int) MatchStatus.InRunning).ToString();

            _snapshot.Markets.Count.Should().Be(0);

            marketsFilter.ApplyRules(_snapshot);

            stateManager.SuspensionManager.SuspendAllMarketsStrategy(marketsFilter.CurrentState);

            plugin.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>(
                    y => y.Markets.Count == mkt_count &&
                    // check selections tradability on each market
                    !y.Markets.Any(z => z.Selections.Any(k => !k.Tradable.HasValue || k.Tradable.Value))),
                    It.IsAny<bool>()));
        }

        [Test]
        public void SuspendAllMarketsWithOutCallingApplyRulesTest()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };

            var settings = new Mock<ISettings>();
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");

            var plugin = new Mock<IAdapterPlugin>();
            var stateManager = new StateManager(settings.Object, plugin.Object);

            // 1) Filter is created with initial snapshot
            var marketsFilter = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);
            _market1.Setup(x => x.Selections).Returns(GetSelections(true, false));
            _market2.Setup(x => x.Selections).Returns(GetSelections(true, false));
            _market3.Setup(x => x.Selections).Returns(GetSelections(true, false));

            marketsFilter.ApplyRules(_snapshot);
            marketsFilter.CommitChanges();

            var mkt_count = _snapshot.Markets.Count;

            _snapshot.Markets.Clear();
            _snapshot.MatchStatus = ((int)MatchStatus.InRunning).ToString();

            _snapshot.Markets.Count.Should().Be(0);

            stateManager.SuspensionManager.SuspendAllMarketsStrategy(marketsFilter.CurrentState);

            plugin.Verify(x => x.ProcessStreamUpdate(It.Is<Fixture>(
                    y => y.Markets.Count == mkt_count &&
                        // check selections tradability on each market
                    !y.Markets.Any(z => z.Selections.Any(k => !k.Tradable.HasValue || k.Tradable.Value))),
                    It.IsAny<bool>()));
        }

        [Test]
        public void RemovePendingMarketsTest()
        {
            // here I want to test that all the markets
            // in a pending state (that never haven't been active before)
            // will be removed from the snapshot through the PendingMarketFilteringRule

            // market1 and market2 are in pending state, while market3 is active
            _market1.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));
            _market2.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));
            _market3.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Active, true));

            var pendingRule = new PendingMarketFilteringRule();
            pendingRule.AddSportToRule("TestFootball");

            List<IMarketRule> rules = new List<IMarketRule> { 
                VoidUnSettledMarket.Instance, 
                InactiveMarketsFilteringRule.Instance,
                pendingRule
            };


            var filteredMarkets = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);

            
            filteredMarkets.ApplyRules(_snapshot);


            _snapshot.Markets.Exists(m => m.Id == _market1.Object.Id).Should().BeFalse();   // market1 should have been removed
            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeFalse();   // market2 should have been removed
            _snapshot.Markets.Exists(m => m.Id == _market3.Object.Id).Should().BeTrue();      // market3 should be there
        }

        [Test]
        public void RemovePendingMarketsWithExcludeListTest()
        {
            // here I want to test that all the markets
            // in a pending state (that never haven't been active before)
            // will be removed from the snapshot through the PendingMarketFilteringRule
            // I also add a list of market type that the rule should not touch

            // market1 and market2 are in pending state, while market3 is active
            _market1.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));
            _market2.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));
            _market3.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Active, true));

            _market1.Object.AddOrUpdateTagValue("type", "do_not_touch");

            var rule = new PendingMarketFilteringRule();
            rule.AddSportToRule("TestFootball");
            rule.ExcludeMarketType("do_not_touch");

            List<IMarketRule> rules = new List<IMarketRule> { 
                rule,
                InactiveMarketsFilteringRule.Instance,
                VoidUnSettledMarket.Instance, 
            };


            var filteredMarkets = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);

            filteredMarkets.ApplyRules(_snapshot);


            _snapshot.Markets.Exists(m => m.Id == _market1.Object.Id).Should().BeTrue();   // market1 has not been touched by InactiveMarketsFilteringRule
            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeFalse();  // market2 should have been removed
            _snapshot.Markets.Exists(m => m.Id == _market3.Object.Id).Should().BeTrue();   // market3 should be there
        }

        [Test]
        public void RemovePendingMarketsWithExcludeListAndInactiveFilterTest()
        {
            // here I want to test that all the markets
            // in a pending state (that never haven't been active before)
            // will be removed from the snapshot through the PendingMarketFilteringRule
            // I also add a list of market type that the rule should not touch

            // market1 and market2 are in pending state, while market3 is active
            _market1.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));
            _market2.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));
            _market3.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Active, true));

            _market1.Object.AddOrUpdateTagValue("type", "do_not_touch");
            _market1.Object.AddOrUpdateTagValue("extra_tag", "just_to_check_that_tags_are_correctly_passed");

            var rule = new PendingMarketFilteringRule();
            rule.AddSportToRule("TestFootball");
            rule.ExcludeMarketType("do_not_touch");

            List<IMarketRule> rules = new List<IMarketRule> { 
                rule,
                InactiveMarketsFilteringRule.Instance,
                VoidUnSettledMarket.Instance, 
            };


            var filteredMarkets = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);

            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            // market1 should not be touched by InactiveMarketsFilteringRule as PendingMarketFilterRule
            // should add it to the list of the "un-removable" markets
            _snapshot.Markets.Exists(m => m.Id == _market1.Object.Id).Should().BeTrue();   
            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeFalse();  // market2 should have been removed
            _snapshot.Markets.Exists(m => m.Id == _market3.Object.Id).Should().BeTrue();   // market3 should be there

            // market 1 and market 2 are now active. As market2 was removed, PendingMarketFiltering rule
            // should add all the tags back to the market
            _market1.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Active, true));
            _market2.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Active, true));
            _market3.Setup(x => x.Selections).Returns(GetSettledSelections());
            _snapshot.Markets.Add(_market2.Object);

            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            _snapshot.Markets.Exists(m => m.Id == _market1.Object.Id).Should().BeTrue();   
            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeTrue();  
            _snapshot.Markets.Exists(m => m.Id == _market3.Object.Id).Should().BeTrue(); 

            _market1.Object.HasTag("extra_tag").Should().BeTrue();
            _market1.Object.GetTagValue("extra_tag").Should().BeEquivalentTo("just_to_check_that_tags_are_correctly_passed");
        }

        [Test]
        public void KeepPendingMarkets()
        {
            // here I want to test that all the markets
            // in a pending state (that they haven't been actived before)
            // will be removed from the snapshot through the PendingMarketFilteringRule

            var pendingRule = new PendingMarketFilteringRule();
            pendingRule.AddSportToRule("TestFootball");

            List<IMarketRule> rules = new List<IMarketRule> { 
                VoidUnSettledMarket.Instance, 
                InactiveMarketsFilteringRule.Instance,
                pendingRule
            };

  
            _market1.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));
            _market2.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));
            _market3.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Active, true));

            var filteredMarkets = new MarketRulesManager(_snapshot.Id, _objectProvider, rules);


            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            _snapshot.Markets.Exists(m => m.Id == _market1.Object.Id).Should().BeFalse();   // market1 should have been removed
            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeFalse();   // market2 should have been removed
            _snapshot.Markets.Exists(m => m.Id == _market3.Object.Id).Should().BeTrue();   // market3 should be there

            _market1.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Active, false));
            _market2.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Active, false));

            _snapshot.Markets.Add(_market1.Object);
            _snapshot.Markets.Add(_market2.Object);

            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            _snapshot.Markets.Exists(m => m.Id == _market1.Object.Id).Should().BeTrue();   // market1 should have been removed
            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeTrue();   // market2 should have been removed
            _snapshot.Markets.Exists(m => m.Id == _market3.Object.Id).Should().BeTrue();   // market3 should be there
        }

        /*[Test]
        public void TortureCacheTest()
        {
            var binaryStore = new BinaryStoreProvider<IDictionary<string, MarketState>>(Environment.CurrentDirectory, "CacheTest-{0}");

            var cacheProvider = new CachedObjectStoreWithPersistance<IDictionary<string, MarketState>>(binaryStore, "Jed", 5);
            bool active = true;

            var snapshot = Helper.GetInPlaySnapshot();
            snapshot.AllMarkets.ForEach(m => m.Selections.ForEach(s => s.Status = !active ? "1" : "0"));
            var marketFilter = new MarketsFilter(snapshot, cacheProvider);
            marketFilter.FilterInactiveMarkets(snapshot);
            marketFilter.CommitChanges();

            for (int i = 0; i < 10; i++)
            {
                snapshot = Helper.GetInPlaySnapshot();
                snapshot.AllMarkets.ForEach(m => m.Selections.ForEach(s => s.Status = active ? "1" : "0"));
                marketFilter.FilterInactiveMarkets(snapshot);
                var sleep = new Random().Next(1, 10);
                Debug.WriteLine(string.Format("Sleeping for {0}", sleep));
                Thread.Sleep(TimeSpan.FromSeconds(sleep));

                // should not be changed at this point
                var marketStates = cacheProvider.GetObject(snapshot.Id);
                marketStates.Values.All(m => m.IsActive == !active).Should().BeTrue();
                
                Debug.WriteLine("Uncommited changes test passed");

                marketFilter.CommitChanges();

                marketStates = cacheProvider.GetObject(snapshot.Id);
                marketStates.Values.All(m => m.IsActive == active).Should().BeTrue();

                Debug.WriteLine("Commited changes test passed");

                active = !active;
            }

            var filePath = Path.Combine(Environment.CurrentDirectory, "CacheTest-" + snapshot.Id);

            Debug.WriteLine(string.Format("File {0}",filePath));
            if (File.Exists(Path.Combine(Environment.CurrentDirectory, "CacheTest-" + snapshot.Id)))
            {
                File.Delete(filePath);
                Debug.WriteLine("File deleted");
            }
        }*/

        private List<Selection> GetSettledSelections()
        {
            var selections = GetSelections(SelectionStatus.Settled, true);
            selections[1].Price = 1;

            return selections;
        }

        private List<Selection> GetSelections(string status,bool isSuspended)
        {
            var selections = new List<Selection>
                {
                    new Selection {Id = "1", Tradable = !isSuspended, Status = status },
                    new Selection {Id = "2", Tradable = !isSuspended, Status = status },
                    new Selection {Id = "3", Tradable = !isSuspended, Status = status }
                };

            return selections;
        }

        private List<Selection> GetSelections(bool isActive, bool isSuspended)
        {
            return GetSelections(isActive ? SelectionStatus.Active : SelectionStatus.Pending, isSuspended);
        }


        /// <summary>
        ///     I want to test that DeltaRule correctly
        ///     filters out markets in a fixture
        ///     when they don't differ from the state
        ///     stored in the MarketRuleManager
        /// </summary>
        [Test]
        [Category("MarketRule")]
        [Category("DeltaRule")]
        public void DeltaRuleTest()
        {
            // STEP 1: prepare stub data
            var settings = new Mock<ISettings>();
            var plugin = new Mock<IAdapterPlugin>();
            var stateprovider = new StateManager(settings.Object, plugin.Object);

            DeltaRule.Instance.Severity = DeltaRule.DeltaRuleSeverity.REMOVE_SELECTIONS;

            List<IMarketRule> rules = new List<IMarketRule> {DeltaRule.Instance};

            // STEP 2: prepare fixture data
            // Fixture
            //  - MKT1
            //  -- SELN_1_1
            //  -- SELN_1_2
            //  - MKT2
            //  -- SELN_2_1
            //  -- SELN_2_2

            Fixture fixture = new Fixture {Id = "TestId", MatchStatus = "40"};
            fixture.Tags.Add("Sport", "Football");

            Market mkt = new Market {Id = "MKT1"};
            mkt.AddOrUpdateTagValue("name", "mkt1");
            mkt.AddOrUpdateTagValue("type", "type1");

            Selection seln = new Selection {Id = "SELN_1_1"};
            seln.AddOrUpdateTagValue("name", "seln_1_1");
            seln.Tradable = true;
            seln.Status = SelectionStatus.Active;
            seln.Price = 0.1;

            mkt.Selections.Add(seln);

            seln = new Selection { Id = "SELN_1_2" };
            seln.AddOrUpdateTagValue("name", "seln_1_2");
            seln.Tradable = true;
            seln.Status = SelectionStatus.Active;
            seln.Price = 0.2;

            mkt.Selections.Add(seln);

            fixture.Markets.Add(mkt);

            mkt = new Market { Id = "MKT2" };
            mkt.AddOrUpdateTagValue("name", "mkt2");
            mkt.AddOrUpdateTagValue("type", "type2");

            seln = new Selection { Id = "SELN_2_1" };
            seln.AddOrUpdateTagValue("name", "seln_2_1");
            seln.Tradable = false;
            seln.Status = SelectionStatus.Pending;
            seln.Price = null;

            mkt.Selections.Add(seln);

            seln = new Selection { Id = "SELN_2_2" };
            seln.AddOrUpdateTagValue("name", "seln_2_2");
            seln.Tradable = false;
            seln.Status = SelectionStatus.Pending;
            seln.Price = null;

            mkt.Selections.Add(seln);

            fixture.Markets.Add(mkt);

            // STEP 3: invoke the delta rule
            MarketRulesManager manager = new MarketRulesManager(fixture.Id, stateprovider, rules);
            
            manager.ApplyRules(fixture); // this will not have any effect as we don't have any state yet

            manager.CommitChanges();

            manager.ApplyRules(fixture); // here is where the delta rule is invoked (note that we haven't changed anything on the fixture)

            manager.CommitChanges();

            // STEP 4: check the results (delta rule should have removed everything)
            fixture.Markets.Count.Should().Be(0);

            // STEP 5: change a single field
            seln.Price = 1.2;
            fixture.Markets.Add(mkt);
            mkt.Selections.Count().Should().Be(2);

            // STEP 6: apply the delta rule again
            manager.ApplyRules(fixture);

            // STEP 7: the market should not have been filtered out
            fixture.Markets.Count().Should().Be(1);
            fixture.Markets[0].Selections.Count().Should().Be(1);
        }

        [Test]
        [Category("MarketRule")]
        [Category("DeltaRule")]
        public void DeltaRuleWithRealFixtureTest()
        {
            var settings = new Mock<ISettings>();
            var plugin = new Mock<IAdapterPlugin>();
            var state = new StateManager(settings.Object, plugin.Object);

            DeltaRule.Instance.Severity = DeltaRule.DeltaRuleSeverity.REMOVE_SELECTIONS;
            List<IMarketRule> rules = new List<IMarketRule> {DeltaRule.Instance};
            state.ClearState("nr7B1f7gjMuk2ggCJoMIizHKrfI");
            var manager = state.CreateNewMarketRuleManager("nr7B1f7gjMuk2ggCJoMIizHKrfI");
            state.AddRules(rules);

            Fixture fixture = TestHelper.GetFixtureFromResource("rugbydata_snapshot_2");

            int mkt_count = fixture.Markets.Count;
            mkt_count.Should().BeGreaterThan(0);

            // first apply should do anything as there is no state
            manager.ApplyRules(fixture);

            fixture.Markets.Count().Should().Be(mkt_count);

            // from now on, we have a state
            manager.CommitChanges();

            Fixture snapshot = TestHelper.GetFixtureFromResource("rugbydata_snapshot_4");

            manager.ApplyRules(snapshot);
            manager.CommitChanges();

            // snapshot 2 and snapshot-4 have no difference, so everything should have been
            // remove by the delta rule
            snapshot.Markets.Count().Should().Be(0); 

        }

        [Test]
        [Category("MarketRule")]
        [Category("DeltaRule")]
        public void DeltaRuleWithMarketSeverity()
        {
            // STEP 1: prepare stub data
            var settings = new Mock<ISettings>();
            var plugin = new Mock<IAdapterPlugin>();
            var stateprovider = new StateManager(settings.Object, plugin.Object);

            DeltaRule.Instance.Severity = DeltaRule.DeltaRuleSeverity.REMOVE_MARKETS;

            List<IMarketRule> rules = new List<IMarketRule> { DeltaRule.Instance };

            // STEP 2: prepare fixture data
            // Fixture
            //  - MKT1
            //  -- SELN_1_1
            //  -- SELN_1_2
            //  - MKT2
            //  -- SELN_2_1
            //  -- SELN_2_2

            Fixture fixture = new Fixture { Id = "TestId", MatchStatus = "40" };
            fixture.Tags.Add("Sport", "Football");

            Market mkt = new Market { Id = "MKT1" };
            mkt.AddOrUpdateTagValue("name", "mkt1");
            mkt.AddOrUpdateTagValue("type", "type1");

            Selection seln = new Selection { Id = "SELN_1_1" };
            seln.AddOrUpdateTagValue("name", "seln_1_1");
            seln.Tradable = true;
            seln.Status = SelectionStatus.Active;
            seln.Price = 0.1;

            mkt.Selections.Add(seln);

            seln = new Selection { Id = "SELN_1_2" };
            seln.AddOrUpdateTagValue("name", "seln_1_2");
            seln.Tradable = true;
            seln.Status = SelectionStatus.Active;
            seln.Price = 0.2;

            mkt.Selections.Add(seln);

            fixture.Markets.Add(mkt);

            mkt = new Market { Id = "MKT2" };
            mkt.AddOrUpdateTagValue("name", "mkt2");
            mkt.AddOrUpdateTagValue("type", "type2");

            seln = new Selection { Id = "SELN_2_1" };
            seln.AddOrUpdateTagValue("name", "seln_2_1");
            seln.Tradable = false;
            seln.Status = SelectionStatus.Pending;
            seln.Price = null;

            mkt.Selections.Add(seln);

            seln = new Selection { Id = "SELN_2_2" };
            seln.AddOrUpdateTagValue("name", "seln_2_2");
            seln.Tradable = false;
            seln.Status = SelectionStatus.Pending;
            seln.Price = null;

            mkt.Selections.Add(seln);

            fixture.Markets.Add(mkt);

            // STEP 3: invoke the delta rule
            MarketRulesManager manager = new MarketRulesManager(fixture.Id, stateprovider, rules);

            manager.ApplyRules(fixture); // this will not have any effect as we don't have any state yet

            manager.CommitChanges();

            manager.ApplyRules(fixture); // here is where the delta rule is invoked (note that we haven't changed anything on the fixture)

            manager.CommitChanges();

            // STEP 4: check the results (delta rule should have removed everything)
            fixture.Markets.Count.Should().Be(0);

            // STEP 5: change a single field
            seln.Price = 1.2;
            fixture.Markets.Add(mkt);
            mkt.Selections.Count().Should().Be(2);

            // STEP 6: apply the delta rule again
            manager.ApplyRules(fixture);

            // STEP 7: the market should not have been filtered out
            fixture.Markets.Count().Should().Be(1);
            fixture.Markets[0].Selections.Count().Should().Be(2);
        }

        /// <summary>
        ///     In this test I want to make sure
        ///     that ApplyPostRulesProcessing() 
        ///     is called on the market state after 
        ///     all the market rules are applied.
        /// </summary>
        [Test]
        [Category("MarketRule")]
        public void PostRuleProcessingTest()
        {

            // This test sets up a dummy market rule that removes
            // all the markets whose Id start with "REMOVE".
            // The test, after calling ApplyRules() on the MarketRuleManager
            // checks if the property "HasBeenProcessed" is set to true
            // for all the markets whose Id don't start with REMOVE.
            //
            // The HasBeenProcessed property is set to true only
            // when a market has been passed at least once to the
            // plugin, or in other words, the outcome of the 
            // MarketRuleManager hasn't remove it from the Fixture object.
            // This property is set on IUpdatableMarketState.ApplyPostRulesProcessing()

            Mock<IMarketRule> aRule = new Mock<IMarketRule>();
            aRule.Setup(x => x.Apply(It.IsAny<Fixture>(), It.IsAny<IMarketStateCollection>(), It.IsAny<IMarketStateCollection>()))
                .Returns((Fixture f, IMarketStateCollection nS, IMarketStateCollection oS) => 
                { 
                    MarketRuleResultIntent intent = new MarketRuleResultIntent();
                    foreach(var mkt in f.Markets)
                    {
                        if(mkt.Id.StartsWith("REMOVE"))
                            intent.MarkAsRemovable(mkt);
                    }

                    return intent;
                }
            );

            var settings = new Mock<ISettings>();
            var plugin = new Mock<IAdapterPlugin>();
            var stateprovider = new StateManager(settings.Object, plugin.Object);

            List<IMarketRule> rules = new List<IMarketRule> { aRule.Object };

            Fixture fixture = new Fixture { Id = "ABC", MatchStatus = "30"};

            Market testMkt = new Market { Id = "1"};
            fixture.Markets.Add(testMkt);

            testMkt = new Market { Id = "REMOVE-1" };
            fixture.Markets.Add(testMkt);

            testMkt = new Market { Id = "2" };
            fixture.Markets.Add(testMkt);

            testMkt = new Market { Id = "REMOVE-2" };
            fixture.Markets.Add(testMkt);

            MarketRulesManager manager = new MarketRulesManager(fixture.Id, stateprovider, rules);

            manager.ApplyRules(fixture);

            manager.CurrentState["1"].Should().NotBeNull();
            manager.CurrentState["1"].HasBeenProcessed.Should().BeTrue();

            manager.CurrentState["2"].Should().NotBeNull();
            manager.CurrentState["2"].HasBeenProcessed.Should().BeTrue();

            manager.CurrentState["REMOVE-1"].Should().NotBeNull();
            manager.CurrentState["REMOVE-1"].HasBeenProcessed.Should().BeFalse();

            manager.CurrentState["REMOVE-2"].Should().NotBeNull();
            manager.CurrentState["REMOVE-2"].HasBeenProcessed.Should().BeFalse();

            manager.CommitChanges();


            manager.CurrentState["1"].Should().NotBeNull();
            manager.CurrentState["1"].HasBeenProcessed.Should().BeTrue();

            manager.CurrentState["2"].Should().NotBeNull();
            manager.CurrentState["2"].HasBeenProcessed.Should().BeTrue();

            manager.CurrentState["REMOVE-1"].Should().NotBeNull();
            manager.CurrentState["REMOVE-1"].HasBeenProcessed.Should().BeFalse();

            manager.CurrentState["REMOVE-2"].Should().NotBeNull();
            manager.CurrentState["REMOVE-2"].HasBeenProcessed.Should().BeFalse();
        }


        /// <summary>
        ///     This test is very similar to PostRuleProcessingTest().
        ///
        ///     The difference is that in here we use real market rules.
        /// 
        /// </summary>
        [Test]
        [Category("MarketRule")]
        public void PostRuleProcessingWithDefaultRulesTest()
        {
            var settings = new Mock<ISettings>();
            var plugin = new Mock<IAdapterPlugin>();
            var stateprovider = new StateManager(settings.Object, plugin.Object);

            var pendingRule = new PendingMarketFilteringRule();
            pendingRule.AddSportToRule("Football");

            List<IMarketRule> rules = new List<IMarketRule>
            {
                VoidUnSettledMarket.Instance,
                pendingRule
            };


            Fixture fixture = new Fixture { Id = "ABCD", MatchStatus = "40" };
            fixture.Tags["Sport"] = "Football";

            Market testMkt = new Market { Id = "1" };
            testMkt.Selections.Add(new Selection { Id= "1-1", Status = SelectionStatus.Pending, Tradable = false});
            fixture.Markets.Add(testMkt);

            testMkt = new Market { Id = "2" };
            testMkt.Selections.Add(new Selection { Id = "2-1", Status = SelectionStatus.Pending, Tradable = false });
            fixture.Markets.Add(testMkt);

            testMkt = new Market { Id = "3" };
            testMkt.Selections.Add(new Selection { Id = "3-1", Status = SelectionStatus.Pending, Tradable = false });
            fixture.Markets.Add(testMkt);

            testMkt = new Market { Id = "4" };
            testMkt.Selections.Add(new Selection { Id = "4-1", Status = SelectionStatus.Pending, Tradable = false });
            fixture.Markets.Add(testMkt);

            MarketRulesManager manager = new MarketRulesManager(fixture.Id, stateprovider, rules);

            manager.ApplyRules(fixture);

            // all the markets should have been removed by the pendinRule
            fixture.Markets.Count().Should().Be(0);

            manager.CommitChanges();

            fixture.Markets.Clear();

            // STEP 2: enable markets "1" and "2"
            testMkt = new Market { Id = "1" };
            testMkt.Selections.Add(new Selection { Id = "1-1", Status = SelectionStatus.Active, Tradable = true });
            fixture.Markets.Add(testMkt);

            testMkt = new Market { Id = "2" };
            testMkt.Selections.Add(new Selection { Id = "2-1", Status = SelectionStatus.Active, Tradable = true });
            fixture.Markets.Add(testMkt);

            manager.ApplyRules(fixture);

            fixture.Markets.Count().Should().Be(2);

            manager.CommitChanges();

            // STEP 3: set fixture match status to "MatchOver" so the VoidUnSettledMarket can kick in

            fixture.Markets.Clear();
            fixture.MatchStatus = "50";
            testMkt = new Market { Id = "1" };
            testMkt.Selections.Add(new Selection { Id = "1-1", Status = SelectionStatus.Active, Tradable = true });

            // as the fixture is matchover, the VoidUnSettledMarket should add the un-settled markets
            // BUT, it should only add the markets that have been processed or NOT been active
            manager.ApplyRules(fixture);

            fixture.Markets.Count().Should().Be(0);
            fixture.Markets.FirstOrDefault(x => x.Id == "1").Should().BeNull(); // because the market has been active
            fixture.Markets.FirstOrDefault(x => x.Id == "2").Should().BeNull(); // because the market has been active
            fixture.Markets.FirstOrDefault(x => x.Id == "3").Should().BeNull(); // because the market has NOT been processed
            fixture.Markets.FirstOrDefault(x => x.Id == "4").Should().BeNull(); // because the market has NOT been processed
        }


        /// <summary>
        /// 
        ///     In this test I want to make sure
        ///     that the IMarketState.Index is correctly set
        ///     as the index value in which the market appears
        ///     in the feed.
        /// 
        /// </summary>
        [Test]
        [Category("MarketRule")]
        public void MarketIndexTest()
        {
            var settings = new Mock<ISettings>();
            var plugin = new Mock<IAdapterPlugin>();
            var stateprovider = new StateManager(settings.Object, plugin.Object);
            var rules = new List<IMarketRule>();

            Fixture fixture = new Fixture { Id = "ABC", MatchStatus = "10"};
            fixture.Tags["Sport"] = "Football";

            fixture.Markets.Add(new Market { Id = "1" });
            fixture.Markets.Add(new Market { Id = "2" });
            fixture.Markets.Add(new Market { Id = "3" });
            fixture.Markets.Add(new Market { Id = "4" });
            fixture.Markets.Add(new Market { Id = "5" });
            fixture.Markets.Add(new Market { Id = "6" });

            MarketRulesManager manager = new MarketRulesManager(fixture.Id, stateprovider, rules);

            manager.ApplyRules(fixture);

            for(int i = 0; i < fixture.Markets.Count; i++)
                manager.CurrentState["" + (i + 1)].Index.Should().Be(i);


            manager.CommitChanges();

            for (int i = 0; i < fixture.Markets.Count; i++)
                manager.CurrentState["" + (i + 1)].Index.Should().Be(i);

        }

        /// <summary>
        /// 
        ///     In this test I want to make sure 
        ///     that the IMarketState.Index is correctly
        ///     set even if a market re-definition is applied.
        /// 
        ///     When a market re-definition is applied, 
        ///     we can have:
        /// 
        ///     1) new markets
        ///     2) old markets get removed
        /// 
        ///     For new markets, the desired behaviour is
        ///     LastIndex + IndexOf(market)
        /// 
        /// </summary>
        [Test]
        [Category("MarketRule")]
        public void MarketIndexOnMarketRedefinitionTest()
        {
            var settings = new Mock<ISettings>();
            var plugin = new Mock<IAdapterPlugin>();
            var stateprovider = new StateManager(settings.Object, plugin.Object);
            var rules = new List<IMarketRule>();

            Fixture fixture = new Fixture { Id = "ABC", MatchStatus = "10" };
            fixture.Tags["Sport"] = "Football";

            fixture.Markets.Add(new Market { Id = "1" });
            fixture.Markets.Add(new Market { Id = "2" });
            fixture.Markets.Add(new Market { Id = "3" });
            fixture.Markets.Add(new Market { Id = "4" });
            fixture.Markets.Add(new Market { Id = "5" });
            fixture.Markets.Add(new Market { Id = "6" });

            MarketRulesManager manager = new MarketRulesManager(fixture.Id, stateprovider, rules);

            manager.ApplyRules(fixture);

            for (int i = 0; i < fixture.Markets.Count; i++)
                manager.CurrentState["" + (i + 1)].Index.Should().Be(i);


            manager.CommitChanges();

            fixture.Markets.Clear();
            fixture.Markets.Add(new Market { Id = "1" });
            fixture.Markets.Add(new Market { Id = "2" });
            fixture.Markets.Add(new Market { Id = "3" });

            // new ones
            fixture.Markets.Add(new Market { Id = "7" });
            fixture.Markets.Add(new Market { Id = "8" });
            fixture.Markets.Add(new Market { Id = "9" });

            manager.ApplyRules(fixture);

            for (int i = 0; i < manager.CurrentState.MarketCount; i++)
            {
                manager.CurrentState["" + (i + 1)].Index.Should().Be(i);
                manager.CurrentState["" + (i + 1)].IsDeleted.Should().Be(i >= 3 && i <=5);
            }

            manager.CommitChanges();

            for (int i = 0; i < manager.CurrentState.MarketCount; i++)
            {
                manager.CurrentState["" + (i + 1)].Index.Should().Be(i);
                manager.CurrentState["" + (i + 1)].IsDeleted.Should().Be(i >= 3 && i <= 5);
            }
        }
    }
}

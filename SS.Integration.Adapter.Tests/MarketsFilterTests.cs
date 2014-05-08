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
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.MarketRules;
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.MarketRules.Model;
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

        private IObjectProvider<IUpdatableMarketStateCollection> _objectProvider;
        private IUpdatableMarketStateCollection _marketStorage = new MarketStateCollection();
            
            
        [SetUp]
        public void SetUp()
        {
            SetUpSnapshotAndMarkets();

            var objectProviderMock = new Mock<IObjectProvider<IUpdatableMarketStateCollection>>();

            var returnDictionary = new Func<IUpdatableMarketStateCollection>(() => _marketStorage);
            objectProviderMock.Setup(x => x.GetObject(It.IsAny<string>())).Returns(returnDictionary);
            
            // if there's a better way of assigning parameter let me know
            objectProviderMock.Setup(x => x.SetObject(It.IsAny<string>(), It.IsAny<IUpdatableMarketStateCollection>()))
                              .Callback<string, IUpdatableMarketStateCollection>((s, newState) => _marketStorage = newState);

            _objectProvider = objectProviderMock.Object;
        }

        [Test]
        public void TestMockSetup()
        {
            ((MarketStateCollection)_marketStorage)["123"] = new MarketState ("123");
            _objectProvider.SetObject("Test999",_marketStorage);
            var test = _objectProvider.GetObject("Test999");
            test.HasMarket("123").Should().BeTrue();
        }

        private void SetUpSnapshotAndMarkets(MatchStatus status = MatchStatus.InRunning)
        {
            _snapshot = new Fixture {Id = "123", MatchStatus = ((int) status).ToString()};
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
            var filteredMarkets = new MarketsRulesManager(_snapshot,_objectProvider, rules);

            _snapshot.MatchStatus = "40";

            // 2) Markets are already created and first update arrives
            filteredMarkets.ApplyRules(_snapshot);

            _snapshot.Markets.Should().Contain(_market1.Object);
            _snapshot.Markets.Should().NotContain(_market2.Object);
            _snapshot.Markets.Should().Contain(_market3.Object);
        }

        [Test]
        public void ShouldRemoveInactiveMarketsFromMultipleUpdates()
        {
            List<IMarketRule> rules = new List<IMarketRule> { VoidUnSettledMarket.Instance, InactiveMarketsFilteringRule.Instance };
            // 1) Filter is created with initial snapshot
            var filteredMarkets = new MarketsRulesManager(_snapshot,_objectProvider, rules);


            // 2) Markets are already created and first update arrives
            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            _snapshot.Markets.Should().Contain(_market1.Object);
            _snapshot.Markets.Should().NotContain(_market2.Object);  // not sent as market2 is still inactive
            _snapshot.Markets.Should().Contain(_market3.Object);

            // 3) New update arrives but market1 is inactive this time
            SetUpSnapshotAndMarkets();
            _market1.Setup(s => s.Selections).Returns(GetSelections(false, false));

            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            _snapshot.Markets.Should().Contain(m=> AreIdsEqual(m,_market1));     // market1 will update with its new status of pending
            _snapshot.Markets.Should().NotContain(m=> AreIdsEqual(m,_market2));  // market2 is still inactive
            _snapshot.Markets.Should().Contain(m=> AreIdsEqual(m,_market3));     // no changes for active market3

            // 4) New update arrives with no changes (market1 is still inactive)
            SetUpSnapshotAndMarkets(); 
            _market1.Setup(s => s.Selections).Returns(GetSelections(false, false));

            filteredMarkets.ApplyRules(_snapshot);
            filteredMarkets.CommitChanges();

            _snapshot.Markets.Should().NotContain(m=> AreIdsEqual(m,_market1));  // market1 will not update as it was inactive before and still inactive
            _snapshot.Markets.Should().NotContain(m=> AreIdsEqual(m,_market2));  // no changes for inactive market2
            _snapshot.Markets.Should().Contain(m=> AreIdsEqual(m,_market3));     // no changes for active market3
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
            var filteredMarkets = new MarketsRulesManager(_snapshot,_objectProvider, rules);

            // 2) Markets are already created and first update arrives with a change in name for inactive market2
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
            var filteredMarkets = new MarketsRulesManager(_snapshot,_objectProvider, rules);

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
            var filteredMarkets = new MarketsRulesManager(_snapshot,_objectProvider, rules);

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
            var filteredMarkets = new MarketsRulesManager(_snapshot,_objectProvider, rules);
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
            var marketsFilter = new MarketsRulesManager(_snapshot, _objectProvider, rules);

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
            var marketsFilter = new MarketsRulesManager(_snapshot, _objectProvider, rules);
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
            var marketsFilter = new MarketsRulesManager(_snapshot, _objectProvider, rules);

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
            var marketsFilter = new MarketsRulesManager(_snapshot, _objectProvider, rules);

            _market1.Setup(x => x.Selections).Returns(GetSettledSelections());
            
            marketsFilter.ApplyRules(_snapshot);
            marketsFilter.CommitChanges();

            _snapshot.Markets.Exists(m => m.Id == _market1.Object.Id).Should().BeTrue();
            _snapshot.Markets.Exists(m => m.Id == _market2.Object.Id).Should().BeFalse();
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
            // 1) Filter is created with initial snapshot
            var marketsFilter = new MarketsRulesManager(_snapshot, _objectProvider, rules);
            _market1.Setup(x => x.Selections).Returns(GetSelections(true, false));
            _market2.Setup(x => x.Selections).Returns(GetSelections(true, false));
            _market3.Setup(x => x.Selections).Returns(GetSelections(true, false));

            marketsFilter.ApplyRules(_snapshot);

            _snapshot.Markets.Count.Should().Be(3);
            foreach (var mkt in _snapshot.Markets)
            {
                mkt.Selections.All(y => y.Tradable.HasValue && y.Tradable.Value).Should().BeTrue();
            }

            var snapshotWithAllMarketsSuspended = marketsFilter.GenerateAllMarketsSuspenssion();

            snapshotWithAllMarketsSuspended.Markets.Count.Should().Be(3);
            snapshotWithAllMarketsSuspended.Markets.All(m => m.IsSuspended).Should().BeTrue();

            foreach (var mkt in snapshotWithAllMarketsSuspended.Markets)
            {
                mkt.Selections.All(x => x.Tradable.HasValue && !x.Tradable.Value).Should().BeTrue();
            }
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

            List<IMarketRule> rules = new List<IMarketRule> { 
                VoidUnSettledMarket.Instance, 
                InactiveMarketsFilteringRule.Instance,
                new PendingMarketFilteringRule()
            };


            var filteredMarkets = new MarketsRulesManager(_snapshot, _objectProvider, rules);

            
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
            rule.ExcludeMarketType("do_not_touch");

            List<IMarketRule> rules = new List<IMarketRule> { 
                rule,
                InactiveMarketsFilteringRule.Instance,
                VoidUnSettledMarket.Instance, 
            };


            var filteredMarkets = new MarketsRulesManager(_snapshot, _objectProvider, rules);

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
            rule.ExcludeMarketType("do_not_touch");

            List<IMarketRule> rules = new List<IMarketRule> { 
                rule,
                InactiveMarketsFilteringRule.Instance,
                VoidUnSettledMarket.Instance, 
            };


            var filteredMarkets = new MarketsRulesManager(_snapshot, _objectProvider, rules);

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

            List<IMarketRule> rules = new List<IMarketRule> { 
                VoidUnSettledMarket.Instance, 
                InactiveMarketsFilteringRule.Instance,
                new PendingMarketFilteringRule()
            };

  
            _market1.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));
            _market2.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Pending, false));
            _market3.Setup(x => x.Selections).Returns(GetSelections(SelectionStatus.Active, true));

            var filteredMarkets = new MarketsRulesManager(_snapshot, _objectProvider, rules);


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
            snapshot.Markets.ForEach(m => m.Selections.ForEach(s => s.Status = !active ? "1" : "0"));
            var marketFilter = new MarketsFilter(snapshot, cacheProvider);
            marketFilter.FilterInactiveMarkets(snapshot);
            marketFilter.CommitChanges();

            for (int i = 0; i < 10; i++)
            {
                snapshot = Helper.GetInPlaySnapshot();
                snapshot.Markets.ForEach(m => m.Selections.ForEach(s => s.Status = active ? "1" : "0"));
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


        [Test]
        [Category("MarketFiltering")]
        public void ShouldCreateValidMarketStateCollection()
        {
            // here I want to test that given a fixture
            // a MarketStateCollection is correctly created

            // STEP 1: prepare data
            Fixture fixture = new Fixture {Id = "ABC"};

            Market market1 = new Market {Id = "Market1"};
            market1.AddOrUpdateTagValue("type", "MarketTypeA");
            market1.AddOrUpdateTagValue("name", "Market1");
            market1.AddOrUpdateTagValue("tagA-1", "A-1");
            market1.AddOrUpdateTagValue("tagA-2", "A-2");
            market1.AddOrUpdateTagValue("tagA-3", "A-3");

            Selection selection1 = new Selection
            {
                Id = "Seln1",
                Tradable = true,
                Status = SelectionStatus.Active,
                Price = 200
            };
            selection1.AddOrUpdateTagValue("name", "Seln1");
            selection1.AddOrUpdateTagValue("tag-S-A-1", "S-A-1");
            selection1.AddOrUpdateTagValue("tag-S-A-2", "S-A-2");
            selection1.AddOrUpdateTagValue("tag-S-A-3", "S-A-3");
            
            market1.Selections.Add(selection1);
            fixture.Markets.Add(market1);


            // STEP 2: create a market state collection
            MarketStateCollection collection = new MarketStateCollection();
            collection.Update(fixture, true);

            // STEP 3: verify results

            collection.HasMarket("Market1").Should().BeTrue();
            collection.MarketCount.Should().Be(1);

            collection["Market1"].Selections.Count().Should().Be(1);

            collection["Market1"].HasBeenActive.Should().BeTrue();
            collection["Market1"].Id.Should().Be("Market1");
            collection["Market1"].IsActive.Should().BeTrue();
            collection["Market1"].IsPending.Should().BeFalse();
            collection["Market1"].IsResulted.Should().BeFalse();
            collection["Market1"].IsSuspended.Should().BeFalse();
            collection["Market1"].Name.Should().Be("Market1");
            collection["Market1"].TagsCount.Should().Be(5);

            collection["Market1"].HasTag("type").Should().BeTrue();
            collection["Market1"].GetTagValue("type").Should().Be("MarketTypeA");
            collection["Market1"].HasTag("name").Should().BeTrue();
            collection["Market1"].GetTagValue("name").Should().Be("Market1");
            collection["Market1"].HasTag("tagA-1").Should().BeTrue();
            collection["Market1"].GetTagValue("tagA-1").Should().Be("A-1");
            collection["Market1"].HasTag("tagA-2").Should().BeTrue();
            collection["Market1"].GetTagValue("tagA-2").Should().Be("A-2");
            collection["Market1"].HasTag("tagA-3").Should().BeTrue();
            collection["Market1"].GetTagValue("tagA-3").Should().Be("A-3");

            collection["Market1"].HasSelection("Seln1").Should().BeTrue();
            collection["Market1"]["Seln1"].Id.Should().Be("Seln1");
            collection["Market1"]["Seln1"].Name.Should().Be("Seln1");
            collection["Market1"]["Seln1"].Price.Should().Be(200);
            collection["Market1"]["Seln1"].Status.Should().Be(SelectionStatus.Active);
            collection["Market1"]["Seln1"].Tradability.Should().BeTrue();
            collection["Market1"]["Seln1"].TagsCount.Should().Be(4);

            collection["Market1"]["Seln1"].HasTag("name").Should().BeTrue();
            collection["Market1"]["Seln1"].GetTagValue("name").Should().Be("Seln1");
            collection["Market1"]["Seln1"].HasTag("tag-S-A-1").Should().BeTrue();
            collection["Market1"]["Seln1"].GetTagValue("tag-S-A-1").Should().Be("S-A-1");
            collection["Market1"]["Seln1"].HasTag("tag-S-A-2").Should().BeTrue();
            collection["Market1"]["Seln1"].GetTagValue("tag-S-A-2").Should().Be("S-A-2");
            collection["Market1"]["Seln1"].HasTag("tag-S-A-3").Should().BeTrue();
            collection["Market1"]["Seln1"].GetTagValue("tag-S-A-3").Should().Be("S-A-3");

        }

        [Test]
        [Category("MarketFiltering")]
        public void ShouldComputeMarketPropertiesCorrectly()
        {
            // here I want to test that MarketState
            // correctly infer a market's status 


            // STEP 1: prepare data
            Market market1 = new Market { Id = "Market1" };
            
            Selection selection1 = new Selection
            {
                Id = "Seln1",
                Tradable = true,
                Status = SelectionStatus.Active,
                Price = 200
            };

            Selection selection2 = new Selection
            {
                Id = "Seln2",
                Tradable = true,
                Status = SelectionStatus.Active,
                Price = 201
            };
            
            market1.Selections.Add(selection1);
            market1.Selections.Add(selection2);

            // STEP 2 : check results

            // case 1: all selections are active and tradability = true
            MarketState state = new MarketState(market1, true);

            state.HasBeenActive.Should().BeTrue();
            state.IsActive.Should().BeTrue();
            state.IsPending.Should().BeFalse();
            state.IsResulted.Should().BeFalse();
            state.IsSuspended.Should().BeFalse();
            
            // case 2: one selection is tradabale = false

            selection2.Tradable = false;

            state = new MarketState(market1, true);

            state.HasBeenActive.Should().BeTrue();
            state.IsActive.Should().BeTrue();
            state.IsPending.Should().BeFalse();
            state.IsResulted.Should().BeFalse();
            state.IsSuspended.Should().BeTrue();   // The market must be suspended

            selection2
        }
    }
}

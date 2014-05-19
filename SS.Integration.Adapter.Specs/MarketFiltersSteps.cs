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
using SS.Integration.Adapter.MarketRules;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;
using TechTalk.SpecFlow;

namespace SS.Integration.Adapter.Specs
{
    [Binding, Scope(Feature = "MarketFilters")]
    public class MarketFiltersSteps
    {
        private IEnumerable<Market> _markets;
        private readonly Fixture _fixture = new Fixture { Id = "TestFixture", MatchStatus = "40", Tags = { { "Sport", "Football" } } };
        private MarketsRulesManager _marketFilters;
        private IMarketStateCollection _marketsCache;

        

        [Given(@"Market with the following selections")]
        public void GivenIMarketWithSelections(Table table)
        {
            _markets = new List<Market> {Helper.GetMarketFromTable(table)};
        }
        
        [When(@"Market filters are initiated")]
        public void WhenMarketFiltersAreInitiated()
        {
            _marketsCache = null;
            _fixture.Markets.Clear();
            _fixture.Markets.AddRange(_markets);
            var objectProviderMock = new Mock<IObjectProvider<IMarketStateCollection>>();
            objectProviderMock.Setup(x => x.GetObject(It.IsAny<string>())).Returns(() => _marketsCache);
            objectProviderMock.Setup(x => x.SetObject(It.IsAny<string>(), It.IsAny<IMarketStateCollection>()))
                .Callback<string, IMarketStateCollection>((s, newState) => _marketsCache = newState);

            List<IMarketRule> rules = new List<IMarketRule> { InactiveMarketsFilteringRule.Instance, VoidUnSettledMarket.Instance };

            _marketFilters = new MarketsRulesManager(_fixture, objectProviderMock.Object, rules);
        }

        [When(@"Market filters are applied")]
        public void WhenMarketFiltersAreApplied()
        {
            _marketFilters.ApplyRules(_fixture);
        }


        [Then(@"Market IsSuspended is (.*)")]
        public void ThenMarketIsSuspendedIs(string isSuspendedString)
        {
            _fixture.Markets.First(m => m.Id == "TestId").IsSuspended.Should().Be(bool.Parse(isSuspendedString));
        }

        [When(@"Update Arrives")]
        public void WhenUpdateArrives(Table table)
        {
            var market = new Market {Id = "TestId"};
            market.AddOrUpdateTagValue("name", "TestMarket");
            market.Selections.Clear();
            market.Selections.AddRange(table.Rows.Select(r=>  Helper.GetObjectFromTableRow<Selection>(r)));

            _fixture.Markets.Clear();
            _fixture.Markets.Add(market);            
        }

        [When(@"Rollback change")]
        public void WhenRollbackChange()
        {
            _marketFilters.RollbackChanges();
        }

        [When(@"Commit change")]
        public void WhenCommitChange()
        {
            _marketFilters.CommitChanges();
        }
        
        [When(@"Request voiding")]
        public void WhenRequestVoiding()
        {
            _marketFilters.ApplyRules(_fixture);           
        }

        [Then(@"Market Voided=(.*)")]
        public void ThenMarketVoidedFalse(bool isVoid)
        {
            var market = _fixture.Markets.First();
            market.IsResulted.Should().Be(isVoid);
        }

        [Then(@"Market with id=(.*) is not removed from snapshot")]
        public void ThenMarketIsNotRemovedFromSnapshot(string marketId)
        {
            _fixture.Markets.Should().Contain(m => m.Id == marketId);
        }

        [Then(@"Market with id=(.*) is removed from snapshot")]
        public void ThenMarketWithIdTestIdIsRemovedFromSnapshot(string marketId)
        {
            _fixture.Markets.Should().NotContain(m => m.Id == marketId);
        }


        [When(@"Fixture is over")]
        public void WhenFixtureIsOver()
        {
            _fixture.MatchStatus = "50";
        }



    }
}

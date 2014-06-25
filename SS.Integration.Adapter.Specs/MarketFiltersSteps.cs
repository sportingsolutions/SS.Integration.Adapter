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
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.MarketRules;
using SS.Integration.Adapter.MarketRules.Interfaces;
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
        private MarketRulesManager _marketFilters;
        private IUpdatableMarketStateCollection _marketsCache;

        

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
            var objectProviderMock = new Mock<IStoredObjectProvider>();
            objectProviderMock.Setup(x => x.GetObject(It.IsAny<string>())).Returns(() => _marketsCache);
            objectProviderMock.Setup(x => x.SetObject(It.IsAny<string>(), It.IsAny<IUpdatableMarketStateCollection>()))
                .Callback<string, IUpdatableMarketStateCollection>((s, newState) => _marketsCache = newState);

            List<IMarketRule> rules = new List<IMarketRule> { InactiveMarketsFilteringRule.Instance, VoidUnSettledMarket.Instance };

            _marketFilters = new MarketRulesManager(_fixture.Id, objectProviderMock.Object, rules);
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


        [Given(@"A market rule with the have the following rules")]
        public void GivenAMarketRuleWithTheHaveTheFollowingRules(Table table)
        {
            Fixture fixture = ScenarioContext.Current["FIXTURE"] as Fixture;
            fixture.Should().NotBeNull();

            Mock<IStoredObjectProvider> provider = new Mock<IStoredObjectProvider>();

            List<IMarketRule> rules = new List<IMarketRule>();
            foreach(var row in table.Rows) 
            {
                Mock<IMarketRule> rule = new Mock<IMarketRule>();
                rule.Setup(x => x.Name).Returns(row["Rule"]);
                rules.Add(rule.Object);
                ScenarioContext.Current.Add("RULE-" + row["Rule"], rule);
            }

            MarketRulesManager manager = new MarketRulesManager(fixture.Id, provider.Object, rules);
            ScenarioContext.Current.Add("MARKETRULEMANAGER", manager);
        }

        [Given(@"the market rules return the following intents")]
        public void GivenTheMarketRulesReturnTheFollowingIntents(Table table)
        {
            MarketRulesManager manager = ScenarioContext.Current["MARKETRULEMANAGER"] as MarketRulesManager;
            manager.Should().NotBeNull();

            Fixture fixture = ScenarioContext.Current["FIXTURE"] as Fixture;
            fixture.Should().NotBeNull();

            foreach (var row in table.Rows)
            {
                var name = row["Rule"];
                Mock<IMarketRule> rule = ScenarioContext.Current["RULE-" + name] as Mock<IMarketRule>;
                rule.Should().NotBeNull();

                var mkt_name = row["Market"];
                var result = row["Result"];
                var mkt = fixture.Markets.FirstOrDefault( x => x.Id == mkt_name);
                mkt.Should().NotBeNull();


                if (!ScenarioContext.Current.ContainsKey("INTENT-RULE-" + name))
                {
                    MarketRuleResultIntent rule_intent = new MarketRuleResultIntent();
                    ScenarioContext.Current.Add("INTENT-RULE-" + name, rule_intent);
                    rule.Setup(x => x.Apply(It.IsAny<Fixture>(), It.IsAny<IMarketStateCollection>(), It.IsAny<IMarketStateCollection>())).Returns(rule_intent);
                }

                MarketRuleResultIntent intent = ScenarioContext.Current["INTENT-RULE-" + name] as MarketRuleResultIntent;
                intent.Should().NotBeNull();

                switch (result)
                {
                    case "E":
                        intent.EditMarket(mkt, x => x.AddOrUpdateTagValue("name",  x.Name + " - E: " + name));
                        break;
                    case "!E":
                        intent.MarkAsUnEditable(mkt);
                        break;
                    case "R":
                        intent.MarkAsRemovable(mkt);
                        break;
                    case "!R":
                        intent.MarkAsUnRemovable(mkt);
                        break;
                    default:
                        throw new Exception("Unknow status");

                }
                
            }
        }


        [Given(@"a fixture with the following markets")]
        public void GivenAFixtureWithTheFollowingMarkets(Table table)
        {
            ScenarioContext.Current.Clear();

            Fixture fixture = new Fixture { Id = "Test" };
            ScenarioContext.Current.Add("FIXTURE", fixture);

            foreach (var row in table.Rows)
            {
                Market mkt = new Market {Id = row["Market"]};
                mkt.AddOrUpdateTagValue("name", row["Name"]);

                fixture.Markets.Add(mkt);
            }
        }

        [When(@"I apply the rules")]
        public void WhenIApplyTheRules()
        {
            MarketRulesManager manager = ScenarioContext.Current["MARKETRULEMANAGER"] as MarketRulesManager;
            manager.Should().NotBeNull();

            Fixture fixture = ScenarioContext.Current["FIXTURE"] as Fixture;

            manager.ApplyRules(fixture);
        }

        [Then(@"I must see these changes")]
        public void ThenIMustSeeTheseChanges(Table table)
        {
            Fixture fixture = ScenarioContext.Current["FIXTURE"] as Fixture;
            fixture.Should().NotBeNull();


            foreach (var row in table.Rows)
            {
                var mkt_name = row["Market"];
                if (Convert.ToBoolean (row["Exists"]))
                {
                    Market mkt = fixture.Markets.First(x => x.Id == mkt_name);
                    var n = row["Name"];
                    mkt.Name.Should().Be(n);
                }
            }
        }

    }
}

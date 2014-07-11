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
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;

namespace SS.Integration.Adapter.Specs
{
    [Binding]
    public class MarketStateSteps
    {
        private Fixture _fixture;
        private MarketStateCollection _marketStateCollection;


        [Given(@"I have this market")]
        public void GivenIHaveThisMarket(Table table)
        {
            Market market = new Market();
            market.Id = "ABC";
            if (table.Rows.Count > 0)
                market.Id = table.Rows[0]["Value"];

            if (ScenarioContext.Current.ContainsKey("Market"))
                ScenarioContext.Current.Remove("Market");

            ScenarioContext.Current.Add("Market", market);
        }
        
        [Given(@"The market has these selections")]
        public void GivenTheMarketHasTheseSelections(Table table)
        {
            if (!ScenarioContext.Current.ContainsKey("Market"))
                return;

            Market market = ScenarioContext.Current["Market"] as Market;
            if (market == null)
                return;

            foreach (var seln in TableToSelections(table))
                market.Selections.Add(seln);
        }
        
        [When(@"I infer the market's status")]
        public void WhenIInferTheMarketSStatus()
        {
            if (!ScenarioContext.Current.ContainsKey("Market"))
                return;

            Market market = ScenarioContext.Current["Market"] as Market;

            MarketState state = new MarketState(market, true);

        }
        
        [Then(@"I should have these values")]
        public void ThenIShouldHaveTheseValues(Table table)
        {
            if (!ScenarioContext.Current.ContainsKey("Market"))
                return;

            Market market = ScenarioContext.Current["Market"] as Market;

            if (table.Rows.Count > 0)
            {
                bool active    = table.Rows[0]["Active"] == "1";
                bool pending = table.Rows[0]["Pending"] == "1";
                bool suspended = table.Rows[0]["Suspended"] == "1";
                bool resulted = table.Rows[0]["Resulted"] == "1";

                market.IsActive.Should().Be(active);
                market.IsPending.Should().Be(pending);
                market.IsSuspended.Should().Be(suspended);
                market.IsResulted.Should().Be(resulted);
            }
        }

        [Given(@"Rolling Handicap market wit id=(.*)")]
        public void GivenRollingHandicapMarketWitIdRollingMarket(string marketId)
        {
            var rollingMarket = new RollingMarket
            {
                Id = marketId
            };

            if (ScenarioContext.Current.ContainsKey("RollingMarket"))
                ScenarioContext.Current.Remove("RollingMarket");

            ScenarioContext.Current.Add("RollingMarket", rollingMarket);
            
            _fixture = new Fixture { Id = "TestFixture", MatchStatus = "40", Tags = { { "Sport", "Football" } } };
            _fixture.Markets.Add(rollingMarket);
        }

        [Given(@"MarketStates collection is set up")]
        public void GivenMarketStatesCollectionIsSetUp()
        {
            _marketStateCollection = new MarketStateCollection("TEST");
        }


        [Given(@"The rolling market has these selections")]
        public void GivenTheRollingMarketHasTheseSelections(Table table)
        {
            if (!ScenarioContext.Current.ContainsKey("RollingMarket"))
                throw new Exception("Market must be set up before we add selections!");

            var rollingHandicapMarket = ScenarioContext.Current["RollingMarket"] as RollingMarket;

            var selections = table.CreateSet<RollingSelection>().ToArray();
            foreach (TableRow row in table.Rows)
            {
                if (row.ContainsKey("TagValue") && row.ContainsKey("TagKey"))
                {
                    //row ids start from 1
                    var id = int.Parse(row.Id())-1;

                    selections[id].AddOrUpdateTagValue(row["TagKey"],row["TagValue"]);
                }
            }

            ((Market) rollingHandicapMarket).Selections.AddRange(selections);

            ScenarioContext.Current["RollingMarket"] = rollingHandicapMarket;
        }

        [When(@"Market has been updated")]
        public void WhenMarketHasBeenUpdated(Table table)
        {
            var rollingHandicap = ScenarioContext.Current["RollingMarket"] as RollingMarket;
            ((Market)rollingHandicap).Selections.Clear();

            var selections = table.CreateSet<RollingSelection>().ToArray();
            ((Market)rollingHandicap).Selections.AddRange(selections);


        }


        [When(@"Market status is generated for snapshot=(.*)")]
        public void WhenMarketStatusIsGeneratedForSnapshot(bool snapshot)
        {
            var rollingHandicapMarket = ScenarioContext.Current["RollingMarket"] as RollingMarket;
            _marketStateCollection.Update(_fixture,snapshot);
        }

        [Then(@"Rolling market should have line=(.*)")]
        public void ThenRollingMarketShouldHaveLine(double line)
        {
            var rollingHandicapMarket = ScenarioContext.Current["RollingMarket"] as RollingMarket;
            rollingHandicapMarket.Line.Should().Be(line);
        }


        private static IEnumerable<Selection> TableToSelections(Table table)
        {
            //return table.CreateSet<Selection>();

            return table.Rows.Select(row => new Selection
            {
                Id = row["Id"],
                Status = row["Status"],
                Tradable = row["Tradability"] == "1",
                Price = Convert.ToDouble(row["Price"])
            });
        }
    }
}

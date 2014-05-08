using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model;
using TechTalk.SpecFlow;

namespace SS.Integration.Adapter.Specs
{
    [Binding]
    public class MarketStateSteps
    {
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


        private static IEnumerable<Selection> TableToSelections(Table table)
        {
            return table.Rows.Select(row => new Selection
            {
                Id = row["Selection"],
                Status = row["Status"],
                Tradable = row["Tradability"] == "1",
                Price = Convert.ToDouble(row["Price"])
            });
        }
    }
}

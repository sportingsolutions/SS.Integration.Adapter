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


using System.Linq;
using log4net;
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules
{
    internal class InactiveMarketsFilteringRule : IMarketRule
    {
        private const string NAME = "Inactive_Markets";
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(InactiveMarketsFilteringRule));
        private static InactiveMarketsFilteringRule _Instance;

        private InactiveMarketsFilteringRule() { }


        public static InactiveMarketsFilteringRule Instance
        {
            get { lock (typeof(InactiveMarketsFilteringRule)) { return _Instance ?? (_Instance = new InactiveMarketsFilteringRule()); } }
        }

        public string Name { get { return NAME; } }

        /// <summary>
        /// Remove markets from snapshot/update when: 
        /// current market state is inactive AND the previous state was inactive too (with no change in status or name)
        /// 
        /// NB: If there is a change in market's name or status then will not be removed
        /// </summary>
        public void Apply(Fixture Fixture, IMarketStateCollection State)
        {
            _Logger.DebugFormat("Filtering inactive markets for {0}", Fixture);


            var inactiveMarkets = Fixture.Markets.Where(m => State.HasMarket(m.Id) && !State[m.Id].IsActive);

            foreach (var market in inactiveMarkets.ToList())
            {

                var marketState = State[market.Id];

                // Only remove market from snapshot/delta if it is not active AND values like name and status have not changed
                if (marketState.IsEqualTo(market))
                {
                    Fixture.Markets.Remove(market);

                    _Logger.InfoFormat("{0} has been removed from {1} due rule={2}", market, Fixture, Name);
                }
            }

            State.Update(Fixture);
        }
    }
}

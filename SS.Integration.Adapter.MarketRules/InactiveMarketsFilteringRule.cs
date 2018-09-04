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
using System.Configuration;
using System.Linq;
using log4net;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules
{
    /// <summary>
    ///    
    ///      This rules removes markets from snapshots/updates if
    /// 
    ///     1) the market is INACTIVE
    ///     2) the market was INACTIVE in the previous update/snapshot
    ///    
    /// </summary>
    public class InactiveMarketsFilteringRule : IMarketRule
    {
        private const string NAME = "Inactive_Markets";
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(InactiveMarketsFilteringRule));
        private static InactiveMarketsFilteringRule _Instance;
        private bool logDetailedMarketRules;

        private InactiveMarketsFilteringRule()
        {
            var value = ConfigurationManager.AppSettings["logDetailedMarketRules"];
            logDetailedMarketRules = string.IsNullOrEmpty(value) && Convert.ToBoolean(value);
        }


        public static InactiveMarketsFilteringRule Instance
        {
            get { lock (typeof(InactiveMarketsFilteringRule)) { return _Instance ?? (_Instance = new InactiveMarketsFilteringRule()); } }
        }

        public string Name { get { return NAME; } }

        /// <summary>
        /// 
        ///     Remove markets from snapshot/update when: 
        ///     current market state is inactive AND the previous state was inactive too (with no change in status or name)
        /// 
        ///     NB: If there is a change in market's name or status then will not be removed
        /// 
        /// </summary>
        public IMarketRuleResultIntent Apply(Fixture fixture, IMarketStateCollection oldState, IMarketStateCollection newState)
        {
            var result = new MarketRuleResultIntent();

            var inactiveMarkets = fixture.Markets.Where(
                m => (oldState != null && oldState.HasMarket(m.Id) && oldState[m.Id].IsEqualTo(newState[m.Id]))
                        && (!m.IsActive && !oldState[m.Id].IsActive));

            var start = DateTime.UtcNow;
            var inactiveList = inactiveMarkets.ToList();


            

                foreach (var market in inactiveList)
            {
                result.MarkAsRemovable(market);
                if (logDetailedMarketRules)
                    _Logger.DebugFormat("market rule={0} => {1} of fixtureId={2} is marked as removable", Name, market, fixture.Id);
            }
            

            _Logger.Info($"Marking markets as removable took marketRulesTime={(DateTime.UtcNow - start).TotalSeconds.ToString("N")} marketsCount={fixture.Markets.Count} removableCount={inactiveList.Count} {fixture}");

            return result;
        }
    }
}

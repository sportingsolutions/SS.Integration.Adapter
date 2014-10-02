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
using log4net;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules
{
    public class VoidUnSettledMarket : IMarketRule
    {
        private const string NAME = "VoidUnSettled_Markets";
        private static readonly ILog _logger = LogManager.GetLogger(typeof(VoidUnSettledMarket));
        private static VoidUnSettledMarket _instance;

        private VoidUnSettledMarket() { }


        public static VoidUnSettledMarket Instance
        {
            get { lock (typeof(VoidUnSettledMarket)) { return _instance ?? (_instance = new VoidUnSettledMarket()); } }
        }

        public string Name { get { return NAME; } }

        public IMarketRuleResultIntent Apply(Fixture fixture, IMarketStateCollection oldState, IMarketStateCollection newState)
        {
         
            var result = new MarketRuleResultIntent();

            if (!fixture.IsMatchOver || oldState == null)
                return result;

            var markets = fixture.Markets.ToDictionary(m => m.Id);

            // get list of markets which are either no longer in snapshot or are in the snapshot and are not resulted
            // markets which were already priced (activated) should be ignored
            var marketsNotPresentInTheSnapshot = new List<IMarketState>();
            foreach (var mkt in newState.Markets)
            {
                IMarketState mkt_state = newState[mkt];
                if (!mkt_state.IsResulted && (!markets.ContainsKey(mkt_state.Id) || !markets[mkt_state.Id].IsResulted))
                    marketsNotPresentInTheSnapshot.Add(mkt_state);
            }
            
            foreach (var mkt_state in marketsNotPresentInTheSnapshot)
            {
                if (mkt_state.HasBeenActive)
                {
                    _logger.WarnFormat("market rule={0} => marketId={1} of {2} was priced during the fixture lifetime but has NOT been settled on match over.", 
                        Name, mkt_state.Id, fixture);
                    continue;
                }

                var market = fixture.Markets.FirstOrDefault(m => m.Id == mkt_state.Id);
                if (market == null)
                {
                    _logger.DebugFormat("market rule={0} => marketId={1} of {2} is marked to be voided", Name, mkt_state.Id, fixture);

                    result.AddMarket(CreateSettledMarket(mkt_state),new MarketRuleAddIntent(MarketRuleAddIntent.OperationType.SETTLE_SELECTIONS));
                }
                else
                {
                    _logger.WarnFormat("market rule={0} => marketId={1} of {2} that was in the snapshot but wasn't resulted is marked to be voided", 
                        Name, market.Id, fixture);

                    Action<Market> action = x => x.Selections.ForEach(s => s.Status = SelectionStatus.Void);
                    MarketRuleEditIntent edit = new MarketRuleEditIntent(action, MarketRuleEditIntent.OperationType.CHANGE_SELECTIONS);
                    result.EditMarket(market, edit);
                }

            }

            return result;
        }

        private static Market CreateSettledMarket(IMarketState MarketState)
        {
            var market = new Market (MarketState.Id);

            if (MarketState.HasTag("line"))
            {
                market.AddOrUpdateTagValue("line", MarketState.GetTagValue("line"));
            }

            foreach (var seln in MarketState.Selections)
                market.Selections.Add(new Selection { Id = seln.Id, Status = SelectionStatus.Void, Price = 0 });

            return market;
        }
    }
}

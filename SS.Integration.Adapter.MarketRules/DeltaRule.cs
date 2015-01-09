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
    public class DeltaRule : IMarketRule
    {
        public enum DeltaRuleSeverity
        {
            REMOVE_MARKETS = 1,
            REMOVE_SELECTIONS = 2
        }

        private const string NAME = "Delta_Rule";
        private readonly ILog _logger = LogManager.GetLogger(typeof(DeltaRule));

        private static DeltaRule _instance;

        private DeltaRule() { Severity = DeltaRuleSeverity.REMOVE_MARKETS;  }

        public DeltaRuleSeverity Severity { get; set; }

        public static DeltaRule Instance
        {
            get { lock (typeof(DeltaRule)) { return _instance ?? (_instance = new DeltaRule()); } }
        }

        public string Name
        {
            get { return NAME; }
        }

        public IMarketRuleResultIntent Apply(Fixture fixture, IMarketStateCollection oldState, IMarketStateCollection newState)
        {
            _logger.DebugFormat("Applying market rule={0} on {1} - severity={2}", Name, fixture, Severity);
            MarketRuleResultIntent intent = new MarketRuleResultIntent();

            // only apply delta rule on a full snapshot
            if (fixture.Tags == null || !fixture.Tags.Any() || oldState == null)
                return intent;


            foreach (var mkt in fixture.Markets)
            {
                if (oldState.HasMarket(mkt.Id))
                {
                    IMarketState state = oldState[mkt.Id];
                    
                    // do not apply the delta rule on markets which have 
                    // been put in a forced suspended state
                    if (state.IsForcedSuspended)
                        continue;

                    if (Severity == DeltaRuleSeverity.REMOVE_SELECTIONS)
                    {
                        ApplyDeltaRule_RemovingSelections(fixture, mkt, state, intent);
                    }
                    else if (Severity == DeltaRuleSeverity.REMOVE_MARKETS)
                    {
                        ApplyDeltaRule_RemovingMarkets(fixture, mkt, state, intent);
                    }
                }
            }

            return intent;
        }

        /// <summary>
        /// This execution of the delta rule CAN remove single selections
        /// from a market if they haven't changes since last time.
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="mkt"></param>
        /// <param name="state"></param>
        /// <param name="intent"></param>
        private void ApplyDeltaRule_RemovingSelections(Fixture fixture, Market mkt, IMarketState state, MarketRuleResultIntent intent)
        {

            List<Selection> seln_changed = new List<Selection>();
            foreach (var seln in mkt.Selections)
            {
                // if we don't have the selection state, we can't
                // determine what has changed within the selection,
                // so we assume that everything has changed
                if (state.HasSelection(seln.Id) && state[seln.Id].IsEquivalentTo(seln, true))
                    continue;

                _logger.DebugFormat("market rule={0} => {1} of {2} has changed", Name, seln, mkt);
                seln_changed.Add(seln);
            }


            if (seln_changed.Count == 0)
            {
                if (state.IsEquivalentTo(mkt, true, false))
                {
                    _logger.DebugFormat("market rule={0} => {1} of {2} marked as removable", Name, mkt, fixture);
                    intent.MarkAsRemovable(mkt);
                }
                else
                {
                    Action<Market> action = x => x.Selections.Clear();
                    MarketRuleEditIntent edit = new MarketRuleEditIntent(action, MarketRuleEditIntent.OperationType.REMOVE_SELECTIONS);
                    intent.EditMarket(mkt, edit);
                }
            }
            else
            {
                if (seln_changed.Count() != mkt.Selections.Count())
                {
                    Action<Market> action = x => x.Selections.RemoveAll(y => !seln_changed.Contains(y));
                    MarketRuleEditIntent edit = new MarketRuleEditIntent(action, MarketRuleEditIntent.OperationType.REMOVE_SELECTIONS);
                    intent.EditMarket(mkt, edit);
                }
            }
        }

        /// <summary>
        /// This execution of the delta rule can only remove markets from the fixture.
        /// This is different from ApplyDeltaRule_RemovingSelections where selections can
        /// be removed from markets.
        /// 
        /// A market is then removed from a fixture if either its tags and all its selections
        /// haven't changed since last time.
        /// 
        /// If only a selection has changed, then entire market, with the remaining selections,
        /// remains untouched by the delta rule.
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="mkt"></param>
        /// <param name="state"></param>
        /// <param name="intent"></param>
        private void ApplyDeltaRule_RemovingMarkets(Fixture fixture, Market mkt, IMarketState state, MarketRuleResultIntent intent)
        {
            if (state.IsEquivalentTo(mkt, true, true))
            {
                intent.MarkAsRemovable(mkt);
                _logger.DebugFormat("market rule={0} => {1} of {2} marked as removable", Name, mkt, fixture);
            }
        }
    }
}

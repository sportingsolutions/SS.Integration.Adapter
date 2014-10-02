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
using log4net;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules
{
    public class PendingMarketFilteringRule : IMarketRule
    {

        private const string NAME = "Pending_Markets";
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(PendingMarketFilteringRule));
        private readonly HashSet<string> _ExcludedMarketType;
        

        public PendingMarketFilteringRule()
        {
            _ExcludedMarketType = new HashSet<string>();
            AlwaysExcludePendingMarkets = false;
        }

        public string Name
        {
            get { return NAME; }
        }

        /// <summary>
        /// Allows to specify a market type that will be 
        /// excluded from the checks performed on this market rule
        /// </summary>
        /// <param name="type"></param>
        public void ExcludeMarketType(string type)
        {
            _ExcludedMarketType.Add(type);
        }

        public void ExcludeMarketType(IEnumerable<string> MarketTypes)
        {
            foreach (var type in MarketTypes)
                ExcludeMarketType(type);
        }

        /// <summary>
        /// By default, this class filters out markets
        /// that are in a pending state and they have
        /// never been on active state.
        /// 
        /// By settting to true this property, the class
        /// will filter out all the pending markets, 
        /// indipendently wheter they have been active
        /// or not.
        /// </summary>
        public bool AlwaysExcludePendingMarkets
        {
            get;
            set;
        }

        public IMarketRuleResultIntent Apply(Fixture fixture, IMarketStateCollection oldState, IMarketStateCollection newState)
        {
            _Logger.DebugFormat("Applying market rule={0} for {1} - AlwaysExcludePendingMarkets={2}", Name, fixture, AlwaysExcludePendingMarkets);

            var result = new MarketRuleResultIntent();

            foreach (var mkt in fixture.Markets)
            {

                if (_ExcludedMarketType.Contains(mkt.Type))
                {
                    _Logger.DebugFormat("market rule={0} => {1} of {2} is marked as un-removable due its type={3}", 
                        Name, mkt, fixture, mkt.Type);

                    result.MarkAsUnRemovable(mkt);
                    continue;
                }

                // get the value from the old state
                IMarketState mkt_state = null;
                if (oldState != null)
                    mkt_state = oldState[mkt.Id];

                // if a market is now active (for the first time), then we add all the tags
                // that we have collected so far and let the market go through the filter
                if (mkt.IsActive && (mkt_state != null && mkt_state.IsPending && !mkt_state.HasBeenActive))
                {
                    GetTags(mkt, mkt_state);
                    result.MarkAsUnRemovable(mkt);

                    _Logger.DebugFormat("market rule={0} => assigned tags to {1} of {2}", Name, mkt, fixture);
                }
                else if (mkt.IsPending)
                {
                    // otherwise, if the market is in a pending state, then we mark it as removable.
                    // This happens if AlwaysExcludePendingMarkets is true, or, if the market
                    // has never been active before
                    if (AlwaysExcludePendingMarkets || (mkt_state != null && !mkt_state.HasBeenActive))
                    {
                        _Logger.DebugFormat("market rule={0} => {1} of {2} is marked as removable", Name, mkt, fixture);

                        result.MarkAsRemovable(mkt);
                    }
                    
                }
            }

            return result;

        }

        private static void GetTags(Market Market, IMarketState State)
        {
            if (State.TagsCount == 0)
                return;

            foreach (var key in State.TagKeys)
            {
                Market.AddOrUpdateTagValue(key, State.GetTagValue(key));
            }

            foreach (var seln in Market.Selections)
            {
                var seln_state = State[seln.Id];

                foreach (var key in seln_state.TagKeys)
                {
                    seln.AddOrUpdateTagValue(key, seln_state.GetTagValue(key));                    
                }
            }
        }
    }
}

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
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules
{
    public class PendingMarketFilteringRule : IMarketRule
    {

        private const string NAME = "PendingMarket_Filtering";
        private readonly HashSet<string> _ExcludedMarketType;
        

        public PendingMarketFilteringRule()
        {
            _ExcludedMarketType = new HashSet<string>();
        }

        public string Name
        {
            get { return NAME; }
        }

        public void ExcludeMarketType(string type)
        {
            _ExcludedMarketType.Add(type);
        }

        public void ExcludeMarketType(IEnumerable<string> MarketTypes)
        {
            foreach (var type in MarketTypes)
                ExcludeMarketType(type);
        }

        public void Apply(Fixture Fixture, IMarketStateCollection OldState, IMarketStateCollection NewState)
        {
            foreach (var mkt in Fixture.Markets)
            {
                if (_ExcludedMarketType.Contains(mkt.Type))
                    continue;

                // get the value from the old state
                var mkt_state = OldState[mkt.Id];

                // here we are trying to filter market that passed from 
                // a pending state to an active state for the first time.
                if (mkt.IsActive && mkt_state.IsPending && !mkt_state.HasBeenActive)
                {
                    GetTags(mkt, mkt_state);
                }
            }

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

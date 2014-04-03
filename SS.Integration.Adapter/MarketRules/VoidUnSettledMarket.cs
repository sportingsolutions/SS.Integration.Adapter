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
using log4net;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules
{
    class VoidUnSettledMarket : IMarketRule
    {
        private const string NAME = "VoidUnSettled_Markets";
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(VoidUnSettledMarket));
        private static VoidUnSettledMarket _Instance;

        private VoidUnSettledMarket() { }


        public static VoidUnSettledMarket Instance
        {
            get { lock (typeof(VoidUnSettledMarket)) { return _Instance ?? (_Instance = new VoidUnSettledMarket()); } }
        }

        public string Name { get { return NAME; } }

        public void Apply(Fixture Fixture, IMarketStateCollection State)
        {
            if (!Fixture.IsMatchOver)
                return;

            var markets = Fixture.Markets.ToDictionary(m => m.Id);

            // get list of markets which are either no longer in snapshot or are in the snpashot and are not resulted
            // markets which were already priced (activated) should be ignored
            var marketsNotPresentInTheSnapshot = new List<IMarketState>();
            foreach (var mkt in State.Markets)
            {
                IMarketState mkt_state = State[mkt];
                if (!mkt_state.IsResulted && (!markets.ContainsKey(mkt_state.Id) || !markets[mkt_state.Id].IsResulted))
                    marketsNotPresentInTheSnapshot.Add(mkt_state);
            }
            
            foreach (var mkt_state in marketsNotPresentInTheSnapshot)
            {
                if (mkt_state.HasBeenActive)
                {
                    _Logger.WarnFormat("marketId={0} of {1} was priced during the fixture lifetime but has NOT been settled on match over.", 
                        mkt_state.Id, Fixture);
                    continue;
                }

                var market = Fixture.Markets.FirstOrDefault(m => m.Id == mkt_state.Id);
                if (market == null)
                {
                    _Logger.DebugFormat(
                        "marketId={0} of {1} will be voided due rule={2}", mkt_state.Id, Fixture, Name);

                    Fixture.Markets.Add(CreateSettledMarket(mkt_state));
                }
                else
                {
                    _Logger.WarnFormat(
                        "Voiding marketId={0} of {1} that was in the snapshot but wasn't resulted. rule={2}", 
                        market.Id, Fixture, Name);

                    market.Selections.ForEach(s => s.Status = SelectionStatus.Void);
                }

            }
        }

        private static Market CreateSettledMarket(IMarketState MarketState)
        {
            var market = new Market { Id = MarketState.Id };

            if (MarketState.Line != null)
            {
                market.Tags.Add("line", MarketState.Line);
            }

            foreach (var seln in MarketState.Selections)
                market.Selections.Add(new Selection { Id = seln.Id, Status = SelectionStatus.Void, Price = 0 });

            return market;
        }
    }
}

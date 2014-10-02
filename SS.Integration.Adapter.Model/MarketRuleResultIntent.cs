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
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Model
{
    public class MarketRuleResultIntent : IMarketRuleResultIntent
    {
        private readonly List<Market> _removables;
        private readonly List<Market> _unRemovables;
        private readonly Dictionary<Market, MarketRuleAddIntent> _added ;
        private readonly List<Market> _unEditables;
        private readonly Dictionary<Market, MarketRuleEditIntent> _edited;

        public MarketRuleResultIntent()
        {
            _removables = new List<Market>();
            _unRemovables = new List<Market>();
            _unEditables = new List<Market>();
            _added = new Dictionary<Market, MarketRuleAddIntent>();
            _edited = new Dictionary<Market, MarketRuleEditIntent>();
        }

        #region IMarketRuleResultIntent

        public IEnumerable<Market> RemovableMarkets
        {
            get { return _removables; }
        }

        public IEnumerable<Market> UnRemovableMarkets
        {
            get { return _unRemovables; }
        }

        public IEnumerable<Market> NewMarkets
        {
            get { return _added.Keys; }
        }

        public IEnumerable<Market> UnEditableMarkets
        {
            get { return _unEditables; }
        }

        public IEnumerable<Market> EditedMarkets
        {
            get { return _edited.Keys; }
        }

        public MarketRuleEditIntent GetEditingAction(Market market)
        {
            return _edited.ContainsKey(market) ? _edited[market] : null;
        }

        public MarketRuleAddIntent GetAddAction(Market mkt)
        {
            return _added[mkt];
        }

        #endregion

        public void MarkAsRemovable(Market market)
        {
            if (market == null || _removables.Contains(market))
                return;

            _removables.Add(market);
        }

        public void MarkAsUnRemovable(Market market)
        {
            if (market == null || _unRemovables.Contains(market))
                return;

            _unRemovables.Add(market);
        }

        public void MarkAsUnEditable(Market market)
        {
            if (market == null || _unEditables.Contains(market))
                return;

            _unEditables.Add(market);
        }

        public void AddMarket(Market market, MarketRuleAddIntent addIntent)
        {
            if (market == null || _added.ContainsKey(market))
                return;

            _added.Add(market,addIntent);
        }

        public void EditMarket(Market market, MarketRuleEditIntent action)
        {
            if (market == null || _edited.ContainsKey(market) || action == null)
                return;

            _edited[market] = action;
        }
    }
}

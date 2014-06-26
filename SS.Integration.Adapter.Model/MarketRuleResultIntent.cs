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
        private readonly List<Market> _Removables;
        private readonly List<Market> _UnRemovables;
        private readonly List<Market> _Added;
        private readonly List<Market> _UnEditables;
        private readonly Dictionary<Market, MarketRuleEditIntent> _Edited;

        public MarketRuleResultIntent()
        {
            _Removables = new List<Market>();
            _UnRemovables = new List<Market>();
            _UnEditables = new List<Market>();
            _Added = new List<Market>();
            _Edited = new Dictionary<Market, MarketRuleEditIntent>();
        }

        #region IMarketRuleResultIntent

        public IEnumerable<Market> RemovableMarkets
        {
            get { return _Removables; }
        }

        public IEnumerable<Market> UnRemovableMarkets
        {
            get { return _UnRemovables; }
        }

        public IEnumerable<Market> NewMarkets
        {
            get { return _Added; }
        }

        public IEnumerable<Market> UnEditableMarkets
        {
            get { return _UnEditables; }
        }

        public IEnumerable<Market> EditedMarkets
        {
            get { return _Edited.Keys; }
        }

        public MarketRuleEditIntent GetEditingAction(Market market)
        {
            return _Edited.ContainsKey(market) ? _Edited[market] : null;
        }

        #endregion

        public void MarkAsRemovable(Market market)
        {
            if (market == null || _Removables.Contains(market))
                return;

            _Removables.Add(market);
        }

        public void MarkAsUnRemovable(Market market)
        {
            if (market == null || _UnRemovables.Contains(market))
                return;

            _UnRemovables.Add(market);
        }

        public void MarkAsUnEditable(Market market)
        {
            if (market == null || _UnEditables.Contains(market))
                return;

            _UnEditables.Add(market);
        }

        public void AddMarket(Market market)
        {
            if (market == null || _Added.Contains(market))
                return;

            _Added.Add(market);
        }

        public void EditMarket(Market market, MarketRuleEditIntent action)
        {
            if (market == null || _Edited.ContainsKey(market) || action == null)
                return;

            _Edited[market] = action;
        }
    }
}

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
using System;

namespace SS.Integration.Adapter.Model
{
    public class MarketRuleResultIntent : IMarketRuleResultIntent
    {
        private readonly List<Market> _MarkedAsRemovable;
        private readonly List<Market> _MarkedAsUnRemovable;
        private readonly List<Market> _Added;
        private readonly List<Market> _MarkedAsUnEditable;
        private readonly Dictionary<Market, Action<Market>> _Edited;

        public MarketRuleResultIntent()
        {
            _MarkedAsRemovable = new List<Market>();
            _MarkedAsUnRemovable = new List<Market>();
            _MarkedAsUnEditable = new List<Market>();
            _Added = new List<Market>();
            _Edited = new Dictionary<Market, Action<Market>>();
        }

        #region IMarketRuleResultIntent

        public IEnumerable<Market> MarkedAsRemovable
        {
            get { return _MarkedAsRemovable; }
        }

        public IEnumerable<Market> MarkedAsUnRemovable
        {
            get { return _MarkedAsUnRemovable; }
        }

        public IEnumerable<Market> Added
        {
            get { return _Added; }
        }

        public IEnumerable<Market> MarkedAsUnEditable
        {
            get { return _MarkedAsUnEditable; }
        }

        public IEnumerable<Market> Edited
        {
            get { return _Edited.Keys; }
        }

        public Action<Market> GetEditingAction(Market Market)
        {
            return _Edited.ContainsKey(Market) ? _Edited[Market] : null;
        }

        #endregion

        public void MarkAsRemovable(Market Market)
        {
            if (Market == null || _MarkedAsRemovable.Contains(Market))
                return;

            _MarkedAsRemovable.Add(Market);
        }

        public void MarkAsUnRemovable(Market Market)
        {
            if (Market == null || _MarkedAsUnRemovable.Contains(Market))
                return;

            _MarkedAsUnRemovable.Add(Market);
        }

        public void MarkAsUnEditable(Market Market)
        {
            if (Market == null || _MarkedAsUnEditable.Contains(Market))
                return;

            _MarkedAsUnEditable.Add(Market);
        }

        public void AddMarket(Market Market)
        {
            if (Market == null || _Added.Contains(Market))
                return;

            _Added.Add(Market);
        }

        public void EditMarket(Market Market, Action<Market> Action)
        {
            if (Market == null || _Edited.ContainsKey(Market) || Action == null)
                return;

            _Edited[Market] = Action;
        }
    }
}

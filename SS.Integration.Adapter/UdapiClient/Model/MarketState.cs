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
using SS.Integration.Adapter.Model;
using SS.Integration.Common;

namespace SS.Integration.Adapter.UdapiClient.Model
{
    [Serializable]
    public class MarketState : ICloneable
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsActive
        {
            get { return _selectionStates.Any(s => s.Value.Status == "1"); }
        }

        public bool IsPending { get; set; }
        public bool IsResulted { get; set; }
        public bool IsSuspended
        {
            get
            {
                return
                    _selectionStates.All(s => s.Value.Status != "1") ||
                    _selectionStates.Where(s => s.Value.Status == "1" && s.Value.Tradability.HasValue).All(s => !s.Value.Tradability.Value);
            }
        }

        public string Line { get; set; }
        public bool IsActivated { get; set; }
        
        /// <summary>
        /// DO NOT USE it's for copying object purpose only!
        /// </summary>
        public MarketState()
        {

        }

        public MarketState(Market market)
        {
            this.Id = market.Id;
            this.UpdateObject(market);

            if(_selectionStates == null)
                _selectionStates = new Dictionary<string, SelectionState>();
        }

        internal void UpdateObject(Market market)
        {
            MergeSelectionStates(market.Selections);
            this.Line = market.Tags != null && market.Tags.ContainsKey("line") ? market.Tags["line"] : Line;
            this.Name = market.Name ?? this.Name;
            this.IsPending = market.IsPending;
            this.IsResulted = market.IsResulted;

            if (!IsActivated && IsActive)
                IsActivated = true;
        }

        private void MergeSelectionStates(List<Selection> selections)
        {
            if(selections == null)
                return;

            if(_selectionStates == null)
                _selectionStates = selections.Select(s => new SelectionState(s)).ToDictionary(key => key.Id);
            else
            {
                foreach (var selection in selections)
                {
                    if(_selectionStates.ContainsKey(selection.Id))
                        _selectionStates[selection.Id].UpdateState(selection);
                    else
                    {
                        _selectionStates[selection.Id] = new SelectionState(selection);
                    }
                }
            }

        }

        private Dictionary<string, SelectionState> _selectionStates; 

     
        public IEnumerable<string> GetSelectionIds()
        {
            return _selectionStates.Select(s => s.Key);
        }

        internal bool IsEqualsTo(Market newMarket)
        {
            if (newMarket == null)
            {
                throw new ArgumentNullException("market");
            }

            if (newMarket.Id != this.Id)
            {
                throw new Exception("Cannot compare two markets with different Ids");
            }

            if (newMarket.Name != null && newMarket.Name != this.Name) return false;

            var currentMarketState = new MarketState(newMarket);

            var isStatusEqual = this.IsPending == currentMarketState.IsPending &&
                                this.IsResulted == currentMarketState.IsResulted &&
                                this.IsSuspended == currentMarketState.IsSuspended &&
                                this.IsActive == currentMarketState.IsActive;
            
            if (isStatusEqual)
            {
                if (Line != null && currentMarketState.Line != null)
                {
                    isStatusEqual = Line == currentMarketState.Line;
                }

                isStatusEqual = isStatusEqual && newMarket.Selections.All(s => _selectionStates.ContainsKey(s.Id) && _selectionStates[s.Id].IsEqualTo(s));
            }

            return isStatusEqual;
        }

        public object Clone()
        {
            var copyObject = Reflection.PropertyCopy<MarketState>.CopyFrom(this) as MarketState;
            copyObject._selectionStates =
                _selectionStates.Values.Select(Reflection.PropertyCopy<SelectionState>.CopyFrom)
                            .ToList()
                            .ToDictionary(s => s.Id);

            return copyObject;
        }
    }
}

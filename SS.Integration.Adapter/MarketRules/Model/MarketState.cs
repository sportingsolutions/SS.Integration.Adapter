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
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules.Model
{
    [Serializable]
    public class MarketState : IMarketState
    {
        private readonly Dictionary<string, ISelectionState> _SelectionStates;
        private Dictionary<string, string> _Tags;

        /// <summary>
        /// DO NOT USE it's for copying object purpose only!
        /// </summary>
        public MarketState() 
        {
            _SelectionStates = new Dictionary<string, ISelectionState>();
            _Tags = new Dictionary<string, string>();
        }

        internal MarketState(string Id)
            :this()
        {
            this.Id = Id;
        }

        public MarketState(Market market, bool fullSnapshot)
            : this(market.Id)
        {
            this.Update(market, fullSnapshot);                
        }

        public string Id { get; private set; }

        public string Name { get { return GetTagValue("name"); } }

        public bool IsActive
        {
            get 
            { 
                return _SelectionStates.Any(s => s.Value.Status == SelectionStatus.Active); 
            }
        }

        public bool IsSuspended
        {
            get
            {
                return
                    _SelectionStates.Where(s => s.Value.Status == SelectionStatus.Active && s.Value.Tradability.HasValue).All(s => !s.Value.Tradability.Value);
            }
        }

        public bool IsPending
        {
            get
            {
                return _SelectionStates.All(s => s.Value.Status == SelectionStatus.Pending);
            }
        }

        public bool IsResulted 
        { 
            get 
            {
                return this.Selections.Any(x => x.Status == SelectionStatus.Settled && x.Price == 1.00) || 
                       this.Selections.All(x => x.Status == SelectionStatus.Void);
            } 
        }

        public bool HasBeenActive { get; set; }
        
        public void Update(Market Market, bool fullSnapshot)
        {
            MergeSelectionStates(Market, fullSnapshot);

            if (fullSnapshot)
            {
                _Tags = new Dictionary<string, string>();
                foreach (var key in Market.TagKeys)
                    _Tags.Add(key, Market.GetTagValue(key));
            }

            if (!HasBeenActive && IsActive)
                HasBeenActive = true;


            Market.IsPending = IsPending;
            Market.IsActive = IsActive;
            Market.IsResulted = IsResulted;
            Market.IsSuspended = IsSuspended;
        }

        private void MergeSelectionStates(Market Market, bool fullSnapshot)
        {
            if (Market.Selections == null)
                return;

            foreach (var selection in Market.Selections)
            {
                if (_SelectionStates.ContainsKey(selection.Id))
                    _SelectionStates[selection.Id].Update(selection, fullSnapshot);
                else
                {
                    _SelectionStates[selection.Id] = new SelectionState(selection, fullSnapshot);
                }
            }
        }

        #region Tags

        public IEnumerable<string> TagKeys
        {
            get
            {
                return _Tags.Keys;
            }
        }

        public bool HasTag(string TagKey)
        {
            return _Tags.ContainsKey(TagKey);
        }

        public string GetTagValue(string TagKey)
        {
            return _Tags.ContainsKey(TagKey) ? _Tags[TagKey] : null;
        }

        public int TagsCount
        {
            get { return _Tags.Count; }
        }

        #endregion

        #region Selections

        public IEnumerable<ISelectionState> Selections
        {
            get
            {
                return _SelectionStates.Keys.Select(seln_id => _SelectionStates[seln_id]);
            }
        }

        public ISelectionState this[string SelectionId]
        {
            get
            {
                return !HasSelection(SelectionId) ? null : _SelectionStates[SelectionId];
            }
        }

        public bool HasSelection(string SelectionId)
        {
            return _SelectionStates.ContainsKey(SelectionId);
        }


        #endregion

        public bool IsEqualTo(Market NewMarket)
        {
            if (NewMarket == null)
                throw new ArgumentNullException("NewMarket");

            if (NewMarket.Id != this.Id)
                throw new Exception("Cannot compare two markets with different Ids");

            if (NewMarket.Name != this.Name) 
                return false;

            var currentMarketState = new MarketState(NewMarket, false);

            var isStatusEqual = this.IsPending == currentMarketState.IsPending &&
                                this.IsResulted == currentMarketState.IsResulted &&
                                this.IsSuspended == currentMarketState.IsSuspended &&
                                this.IsActive == currentMarketState.IsActive;
            
            if (isStatusEqual)
            {
                if (this.HasTag("line") && currentMarketState.HasTag("line"))
                {
                    isStatusEqual = string.Equals(this.GetTagValue("line"), currentMarketState.GetTagValue("line"));
                }

                isStatusEqual = isStatusEqual && NewMarket.Selections.All(s => _SelectionStates.ContainsKey(s.Id) && _SelectionStates[s.Id].IsEqualTo(s));
            }

            return isStatusEqual;
        }

        public IMarketState Clone()
        {
            MarketState clone = new MarketState
            {
                Id = this.Id,
                HasBeenActive = this.HasBeenActive
            };

            foreach(var key in this.TagKeys)
                clone._Tags.Add(key, this.GetTagValue(key));

            foreach (var seln in this.Selections)
                clone._SelectionStates.Add(seln.Id, seln.Clone());

            return clone;
        }
    }
}

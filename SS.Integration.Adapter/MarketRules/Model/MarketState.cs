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
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules.Model
{
    [Serializable]
    internal class MarketState : IUpdatableMarketState
    {
        private readonly Dictionary<string, IUpdatableSelectionState> _SelectionStates;
        private Dictionary<string, string> _Tags;

        /// <summary>
        /// DO NOT USE it's for copying object purpose only!
        /// </summary>
        public MarketState() 
        {
            _SelectionStates = new Dictionary<string, IUpdatableSelectionState>();
            _Tags = new Dictionary<string, string>();
        }

        internal MarketState(string Id)
            : this()
        {
            this.Id = Id;
        }

        public MarketState(Market market, bool fullSnapshot)
            : this(market.Id)
        {
            this.Update(market, fullSnapshot);                
        }


        #region IMarketState

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
                return _SelectionStates.Where(s => s.Value.Status == SelectionStatus.Active && s.Value.Tradability.HasValue).All(s => !s.Value.Tradability.Value);
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
                // market is resulted if at least one selection has a price of 1.0 and status settled and the rest are settled or void
                // alternatively all selections need to be void
                return (Selections.Any(x => x.Status == SelectionStatus.Settled && x.Price == 1.0) &&
                        Selections.All(x => x.Status == SelectionStatus.Settled || x.Status == SelectionStatus.Void)) ||
                        Selections.All(x => x.Status == SelectionStatus.Void);
            } 
        }

        public bool IsTradedInPlay { get; set; }

        public bool HasBeenActive { get; set; }
        
        public void Update(Market Market, bool fullSnapshot)
        {
            MergeSelectionStates(Market, fullSnapshot);

            if (fullSnapshot)
            {
                _Tags = new Dictionary<string, string>();
                foreach (var key in Market.TagKeys)
                    _Tags.Add(key, Market.GetTagValue(key));

                if (_Tags.ContainsKey("traded_in_play"))
                    IsTradedInPlay = string.Equals(_Tags["traded_in_play"], "true", StringComparison.OrdinalIgnoreCase);
            }

            if (!HasBeenActive && IsActive)
                HasBeenActive = true;


            Market.IsPending = IsPending;
            Market.IsActive = IsActive;
            Market.IsResulted = IsResulted;
            Market.IsSuspended = IsSuspended;
            Market.IsTradedInPlay = IsTradedInPlay;
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
            get { return _SelectionStates.Values; }
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

        #endregion

        #region IUpdatableMarketState

        public bool IsEqualTo(IMarketState NewMarket)
        {
            if (NewMarket == null)
                throw new ArgumentNullException("NewMarket");

            if (NewMarket.Id != this.Id)
                throw new Exception("Cannot compare two markets with different Ids");

            if (NewMarket.Name != this.Name)
                return false;

            var isStatusEqual = this.IsPending == NewMarket.IsPending &&
                                this.IsResulted == NewMarket.IsResulted &&
                                this.IsSuspended == NewMarket.IsSuspended &&
                                this.IsActive == NewMarket.IsActive;

            if (isStatusEqual)
            {
                if (this.HasTag("line") && NewMarket.HasTag("line"))
                {
                    isStatusEqual = string.Equals(this.GetTagValue("line"), NewMarket.GetTagValue("line"));
                }

                isStatusEqual = isStatusEqual && NewMarket.Selections.All(s => _SelectionStates.ContainsKey(s.Id) && _SelectionStates[s.Id].IsEqualTo(s));
            }

            return isStatusEqual;
        } 

        public IUpdatableMarketState Clone()
        {
            MarketState clone = new MarketState
            {
                Id = this.Id,
                HasBeenActive = this.HasBeenActive,
                IsTradedInPlay = this.IsTradedInPlay
            };

            foreach(var key in this.TagKeys)
                clone._Tags.Add(key, this.GetTagValue(key));

            foreach (var seln in this._SelectionStates.Values)
                clone._SelectionStates.Add(seln.Id, seln.Clone());

            return clone;
        }

        #endregion
    }
}

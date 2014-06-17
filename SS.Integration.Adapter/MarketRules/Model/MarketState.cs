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
        private readonly Dictionary<string, IUpdatableSelectionState> _selectionStates;
        private Dictionary<string, string> _Tags;

        /// <summary>
        /// DO NOT USE it's for copying object purpose only!
        /// </summary>
        public MarketState() 
        {
            _selectionStates = new Dictionary<string, IUpdatableSelectionState>();
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
                return _selectionStates.Any(s => s.Value.Status == SelectionStatus.Active); 
            }
        }

        public bool IsSuspended
        {
            get
            {
                return _selectionStates.Where(s => s.Value.Status == SelectionStatus.Active && s.Value.Tradability.HasValue).All(s => !s.Value.Tradability.Value);
            }
        }

        public bool IsPending
        {
            get
            {
                return _selectionStates.All(s => s.Value.Status == SelectionStatus.Pending);
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
        
        public void Update(Market market, bool fullSnapshot)
        {
            MergeSelectionStates(market, fullSnapshot);

            if (fullSnapshot)
            {
                _Tags = new Dictionary<string, string>();
                foreach (var key in market.TagKeys)
                    _Tags.Add(key, market.GetTagValue(key));

                if (_Tags.ContainsKey("traded_in_play"))
                    IsTradedInPlay = string.Equals(_Tags["traded_in_play"], "true", StringComparison.OrdinalIgnoreCase);
            }

            if (!HasBeenActive && IsActive)
                HasBeenActive = true;


            market.IsPending = IsPending;
            market.IsActive = IsActive;
            market.IsResulted = IsResulted;
            market.IsSuspended = IsSuspended;
            market.IsTradedInPlay = IsTradedInPlay;

            UpdateLineOnRollingHandicap(market);
        }

        private void UpdateLineOnRollingHandicap(Market market)
        {
            var rollingHandicap = market as RollingMarket;
            if(rollingHandicap == null)
                return;

            var oneLine = _selectionStates.Values.All(s=> s.Line == _selectionStates.Values.First().Line);

            if (oneLine)
                rollingHandicap.Line = rollingHandicap.Selections.First().Line;
            else
            {
                var selectionWithHomeTeam =
                    _selectionStates.Values.FirstOrDefault(s => s.HasTag("team") && s.GetTagValue("team") == "1");
                if(selectionWithHomeTeam == null)
                    throw new ArgumentException(string.Format("Rolling handicap line for market {0} can't be verified",market));

                var homeSelection = rollingHandicap.Selections.FirstOrDefault(s => s.Id == selectionWithHomeTeam.Id);
                
                //during update we may not receive an update for all selections
                if (homeSelection != null)
                {
                    rollingHandicap.Line = homeSelection.Line;
                }
                else
                {
                    var selectionWithAwayTeam =
                        _selectionStates.Values.FirstOrDefault(s => s.HasTag("team") && s.GetTagValue("team") == "2");

                    // invert the line
                    rollingHandicap.Line = selectionWithAwayTeam.Line*(-1);
                }
            }

        }

        private void MergeSelectionStates(Market market, bool fullSnapshot)
        {
            if (market.Selections == null)
                return;

            foreach (var selection in market.Selections)
            {
                if (_selectionStates.ContainsKey(selection.Id))
                    _selectionStates[selection.Id].Update(selection, fullSnapshot);
                else
                {
                    _selectionStates[selection.Id] = new SelectionState(selection, fullSnapshot);
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
            get { return _selectionStates.Values; }
        }

        public ISelectionState this[string selectionId]
        {
            get
            {
                return !HasSelection(selectionId) ? null : _selectionStates[selectionId];
            }
        }

        public bool HasSelection(string selectionId)
        {
            return _selectionStates.ContainsKey(selectionId);
        }


        #endregion

        #endregion

        #region IUpdatableMarketState

        public bool IsEqualTo(IMarketState newMarket)
        {
            if (newMarket == null)
                throw new ArgumentNullException("newMarket");

            if (newMarket.Id != this.Id)
                throw new Exception("Cannot compare two markets with different Ids");

            if (newMarket.Name != this.Name)
                return false;

            var isStatusEqual = this.IsPending == newMarket.IsPending &&
                                this.IsResulted == newMarket.IsResulted &&
                                this.IsSuspended == newMarket.IsSuspended &&
                                this.IsActive == newMarket.IsActive;

            if (isStatusEqual)
            {
                if (this.HasTag("line") && newMarket.HasTag("line"))
                {
                    isStatusEqual = string.Equals(this.GetTagValue("line"), newMarket.GetTagValue("line"));
                }

                isStatusEqual = isStatusEqual && newMarket.Selections.All(s => _selectionStates.ContainsKey(s.Id) && _selectionStates[s.Id].IsEqualTo(s));
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

            foreach (var seln in this._selectionStates.Values)
                clone._selectionStates.Add(seln.Id, seln.Clone());

            return clone;
        }

        #endregion
    }
}

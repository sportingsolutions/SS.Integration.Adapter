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
        private Dictionary<string, string> _tags;

        /// <summary>
        /// DO NOT USE it's for copying object purpose only!
        /// </summary>
        public MarketState() 
        {
            _selectionStates = new Dictionary<string, IUpdatableSelectionState>();
            _tags = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }

        internal MarketState(string Id)
            : this()
        {
            this.Id = Id;
        }

        public MarketState(Market market, bool fullSnapshot)
            : this(market.Id)
        {
            IsRollingMarket = market is RollingMarket;
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
                        IsVoided;
            } 
        }

        public bool IsVoided
        {
            get
            {
                return Selections.All(x => x.Status == SelectionStatus.Void);
            }
        }

        public bool IsTradedInPlay { get; set; }

        public bool HasBeenActive { get; set; }

        public bool IsForcedSuspended { get; private set; }

        public bool IsRollingMarket { get; private set; }

        public double? Line { get; set; }

        public bool IsDeleted { get; set; }

        #region Tags

        public IEnumerable<string> TagKeys
        {
            get
            {
                return _tags.Keys;
            }
        }

        public bool HasTag(string TagKey)
        {
            return _tags.ContainsKey(TagKey);
        }

        public string GetTagValue(string TagKey)
        {
            return _tags.ContainsKey(TagKey) ? _tags[TagKey] : null;
        }

        public int TagsCount
        {
            get { return _tags.Count; }
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

        public int SelectionsCount
        {
            get { return _selectionStates.Count; }
        }

        #endregion

        public bool IsEqualTo(IMarketState marketState)
        {
            if (marketState == null)
                throw new ArgumentNullException("marketState");

            if (ReferenceEquals(this, marketState))
                return true;
            
            if (marketState.Id != this.Id)
                throw new Exception("Cannot compare two markets with different Ids");

            if (marketState.Name != this.Name)
                return false;

            var isStatusEqual = this.IsPending == marketState.IsPending &&
                                this.IsResulted == marketState.IsResulted &&
                                this.IsSuspended == marketState.IsSuspended &&
                                this.IsActive == marketState.IsActive &&
                                this.IsDeleted == marketState.IsDeleted &&
                                this.IsForcedSuspended == marketState.IsForcedSuspended &&
                                this.IsVoided == marketState.IsVoided;
            

            if (isStatusEqual)
            {
                if (this.HasTag("line") && marketState.HasTag("line"))
                {
                    isStatusEqual = string.Equals(this.GetTagValue("line"), marketState.GetTagValue("line"));
                }


                if (IsRollingMarket)
                {
                    isStatusEqual &= Line == marketState.Line;
                }

                isStatusEqual = isStatusEqual && marketState.Selections.All(s => _selectionStates.ContainsKey(s.Id) && _selectionStates[s.Id].IsEqualTo(s));
            }

            return isStatusEqual;
        }

        public bool IsEquivalentTo(Market market, bool checkTags, bool checkSelections)
        {
            if (market == null)
                return false;

            if (market.Id != Id)
                return false;

            if(checkTags)
            {
                if (market.TagsCount != TagsCount)
                    return false;

                if (market.TagKeys.Any(tag => !HasTag(tag) || GetTagValue(tag) != market.GetTagValue(tag)))
                    return false;
            }

            if (checkSelections)
            {
                if (Selections.Count() != market.Selections.Count())
                    return false;

                foreach (var seln in market.Selections)
                {
                    ISelectionState seln_state = this[seln.Id];
                    if (seln_state == null)
                        return false;

                    if (!seln_state.IsEquivalentTo(seln, checkTags))
                        return false;
                }
            }

            var result = IsSuspended == market.IsSuspended &&
                         IsActive == market.IsActive &&
                         IsResulted == market.IsResulted &&
                         IsPending == market.IsPending &&
                         IsVoided == market.IsVoided;


            if (IsRollingMarket)
                result &= Line == ((RollingMarket)market).Line;

            // the only case we really should pay attention
            // is when we have forced the suspension
            // when the market was active
            if(IsForcedSuspended)
                result &= market.IsSuspended;
            
            return result;
        }

        #endregion

        #region IUpdatableMarketState

        public void Update(Market market, bool fullSnapshot)
        {
            MergeSelectionStates(market, fullSnapshot);

            if (fullSnapshot)
            {
                _tags = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (var key in market.TagKeys)
                    _tags.Add(key, market.GetTagValue(key));

                if (_tags.ContainsKey("traded_in_play"))
                    IsTradedInPlay = string.Equals(_tags["traded_in_play"], "true", StringComparison.InvariantCultureIgnoreCase);
            }

            if (!HasBeenActive && IsActive)
                HasBeenActive = true;

            // always set to false at each update
            IsForcedSuspended = false;

            market.IsPending = IsPending;
            market.IsActive = IsActive;
            market.IsResulted = IsResulted;
            market.IsSuspended = IsSuspended;
            market.IsTradedInPlay = IsTradedInPlay;
            market.IsVoided = IsVoided;

            UpdateLineOnRollingHandicap(market);
        }

        public IUpdatableMarketState Clone()
        {
            MarketState clone = new MarketState
            {
                Id = this.Id,
                HasBeenActive = this.HasBeenActive,
                IsTradedInPlay = this.IsTradedInPlay,
                IsRollingMarket = this.IsRollingMarket,
                IsDeleted = this.IsDeleted,
                Line = this.Line,
                IsForcedSuspended = this.IsForcedSuspended
            };

            foreach(var key in this.TagKeys)
                clone._tags.Add(key, this.GetTagValue(key));

            foreach (var seln in this._selectionStates.Values)
                clone._selectionStates.Add(seln.Id, seln.Clone());

            return clone;
        }

        public void SetForcedSuspensionState(bool isSuspended = true)
        {
            IsForcedSuspended = isSuspended;
        }

        #endregion

        private void UpdateLineOnRollingHandicap(Market market)
        {
            var rollingHandicap = market as RollingMarket;
            if (rollingHandicap == null)
                return;

            var oneLine = _selectionStates.Values.All(s => s.Line == _selectionStates.Values.First().Line);

            if (oneLine)
                rollingHandicap.Line = rollingHandicap.Selections.First().Line;
            else
            {
                var selectionWithHomeTeam =
                    _selectionStates.Values.FirstOrDefault(s => s.HasTag("team") && s.GetTagValue("team") == "1");
                if (selectionWithHomeTeam == null)
                    throw new ArgumentException(string.Format("Rolling handicap line for market {0} can't be verified", market));

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
                    rollingHandicap.Line = selectionWithAwayTeam.Line * (-1);
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

        public override string ToString()
        {
            return string.Format("Market marketId={0} marketName={1}", Id, Name);
        }
    }
}

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
    internal class SelectionState : IUpdatableSelectionState
    {
        private Dictionary<string, string> _tags;        

        /// <summary>
        /// DO NOT USE - this constructor is for copying object only
        /// </summary>
        public SelectionState() 
        {
            _tags = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public SelectionState(Selection selection, bool fullSnapshot)
            : this()
        {
            Id = selection.Id;
            IsRollingSelection = selection is RollingSelection;
            Update(selection, fullSnapshot);
        }

        #region ISelectionState

        public string Id { get; private set; }

        public string Name { get; private set; }

        public double? Price { get; private set; }

        public bool? Tradability { get; private set; }

        public string Status { get; private set; }
        
        public double? Line { get; private set; }

        public bool IsRollingSelection { get; private set; }

        #region Tags

        public IEnumerable<string> TagKeys
        {
            get { return _tags.Keys; }
        }

        public string GetTagValue(string tagKey)
        {
            return _tags.ContainsKey(tagKey) ? _tags[tagKey] : null;
        }

        public int TagsCount
        {
            get { return _tags.Count; }
        }

        public bool HasTag(string tagKey)
        {
            return !string.IsNullOrEmpty(tagKey) && _tags.ContainsKey(tagKey);
        }

        #endregion

        public bool IsEqualTo(ISelectionState selection)
        {
            if (selection == null)
                throw new ArgumentNullException("selection", "selection is null in SelectionState comparison");

            if (ReferenceEquals(this, selection))
                return true;

            if (selection.Id != Id)
                throw new Exception("Cannot compare two selections with different Ids");

            bool ret = (selection.Name == null || selection.Name == this.Name) &&
                       this.Price == selection.Price &&
                       this.Tradability == selection.Tradability &&
                       this.Status == selection.Status;

            if (IsRollingSelection)
                ret &= Line == selection.Line;

            return ret;
        }

        public bool IsEquivalentTo(Selection selection, bool checkTags)
        {
            if (selection == null)
                return false;

            if (selection.Id != Id)
                return false;

            if (checkTags)
            {
                if (selection.TagsCount != TagsCount)
                    return false;


                if (selection.TagKeys.Any(tag => !HasTag(tag) || GetTagValue(tag) != selection.GetTagValue(tag)))
                    return false;

                // if we are here, there is no difference between the stored tags
                // and those contained within the selection object...
                // we can then proceed to check the selection's fields
                // Note that Selection.Name is a shortcut for Selection.GetTagValue("name")
            }

            var result = Price == selection.Price &&
                         Status == selection.Status &&
                         Tradability == selection.Tradable;

            
            if (IsRollingSelection)
                return result &= Line == ((RollingSelection)selection).Line;

            return result;
        }

        #endregion

        #region IUpdatableSelectionState

        public void Update(Selection selection, bool fullSnapshot)
        {
            Price = selection.Price;
            Status = selection.Status;
            Tradability = selection.Tradable;

            if (fullSnapshot)
            {
                _tags = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                foreach (var key in selection.TagKeys)
                    _tags.Add(key, selection.GetTagValue(key));

                Name = selection.Name;
            }

            UpdateLineOnRollingSelection(selection);
        }

        private void UpdateLineOnRollingSelection(Selection selection)
        {
            var rollingSelection = selection as RollingSelection;
            if(rollingSelection == null) 
                return;

            this.Line = rollingSelection.Line;
        }

        public IUpdatableSelectionState Clone()
        {
            SelectionState clone = new SelectionState
            {
                Id = this.Id,
                Name = this.Name,
                Price = this.Price,
                Tradability = this.Tradability,
                Status = this.Status,
                Line = this.Line,
                IsRollingSelection = this.IsRollingSelection
            };

            foreach (var key in this.TagKeys)
                clone._tags.Add(key, this.GetTagValue(key));

            return clone;
        }

        #endregion
    }
}

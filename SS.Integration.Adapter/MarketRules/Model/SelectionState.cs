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
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules.Model
{
    [Serializable]
    internal class SelectionState : IUpdatableSelectionState
    {
        private Dictionary<string, string> _Tags;

        /// <summary>
        /// DO NOT USE - this constructor is for copying object only
        /// </summary>
        public SelectionState() 
        {
            _Tags = new Dictionary<string, string>();
        }

        public SelectionState(Selection selection, bool fullSnapshot)
            : this()
        {
            Id = selection.Id;
            Update(selection, fullSnapshot);
        }

        #region ISelectionState

        public string Id { get; private set; }

        public string Name { get; private set; }

        public double? Price { get; private set; }

        public bool? Tradability { get; private set; }

        public string Status { get; private set; }
        
        public double Line { get; private set; }

        #region Tags

        public IEnumerable<string> TagKeys
        {
            get { return _Tags.Keys; }
        }

        public string GetTagValue(string TagKey)
        {
            return _Tags.ContainsKey(TagKey) ? _Tags[TagKey] : null;
        }

        public int TagsCount
        {
            get { return _Tags.Count; }
        }

        public bool HasTag(string TagKey)
        {
            return !string.IsNullOrEmpty(TagKey) && _Tags.ContainsKey(TagKey);
        }

        #endregion

        #endregion

        public bool IsEqualTo(ISelectionState Selection)
        {
            if (this == Selection)
                return true;

            if (Selection == null)
                throw new ArgumentNullException("Selection", "Selection is null in SelectionState comparison");

            return (Selection.Name == null || Selection.Name == this.Name)
                   && this.Price == Selection.Price
                   && this.Tradability == Selection.Tradability
                   && this.Status == Selection.Status;
        }

        #region IUpdatableSelectionState

        public void Update(Selection selection, bool fullSnapshot)
        {
            Price = selection.Price;
            Status = selection.Status;
            Tradability = selection.Tradable;

            if (fullSnapshot)
            {
                _Tags = new Dictionary<string, string>();

                foreach (var key in selection.TagKeys)
                    _Tags.Add(key, selection.GetTagValue(key));

                Name = selection.Name;
            }

            UpdateLineOnRollingSelection(selection);
        }

        private void UpdateLineOnRollingSelection(Selection selection)
        {
            var rollingSelection = selection as RollingSelection;
            if(rollingSelection == null) return;

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
                Status = this.Status
            };

            foreach (var key in this.TagKeys)
                clone._Tags.Add(key, this.GetTagValue(key));

            return clone;
        }

        #endregion
    }
}

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
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules.Model
{
    [Serializable]
    public class SelectionState : ISelectionState
    {
        private Dictionary<string, object> _Tags;

        /// <summary>
        /// DO NOT USE - this constructor is for copying object only
        /// </summary>
        public SelectionState() 
        {
            _Tags = new Dictionary<string, object>();
        }

        public SelectionState(Selection selection)
            : this()
        {
            Id = selection.Id;
            Update(selection);
        }

        public string Id { get; private set; }

        public string Name { get; private set; }

        public double? Price { get; private set; }

        public bool? Tradability { get; private set; }

        public string Status { get; private set; }

        public void Update(Selection Selection)
        {
            Price = Selection.Price;
            Status = Selection.Status;
            Tradability = Selection.Tradable;

            if (Selection.Tags != null)
            {
                _Tags = new Dictionary<string, object>();

                foreach (var key in Selection.Tags.Keys)
                    _Tags.Add(key, Selection.Tags[key]);

                Name = Selection.Name;
            }
        }

        public bool IsEqualTo(Selection Selection)
        {
            if(Selection == null)
                throw new ArgumentNullException("Selection", "selection is null in SelectionState comparison");

            return (Selection.Name == null || Selection.Name == this.Name)
                   && this.Price == Selection.Price
                   && this.Tradability == Selection.Tradable
                   && this.Status == Selection.Status;
        }

        public IEnumerable<string> TagKeys
        {
            get { return _Tags.Keys; }
        }

        public object GetTagValue(string TagKey)
        {
            return _Tags.ContainsKey(TagKey) ? _Tags[TagKey] : null;
        }

        public ISelectionState Clone()
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
            {
                // not a problem not cloning the tag's value
                clone._Tags.Add(key, this.GetTagValue(key));
            }

            return clone;
        }
    }
}

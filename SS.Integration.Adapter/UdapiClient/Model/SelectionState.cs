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
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter
{
    [Serializable]
    public class SelectionState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double? Price { get; set; }
        public bool? Tradability { get; set; }
        public string Status { get; set; }

        /// <summary>
        /// DO NOT USE - this constructor is for copying object only
        /// </summary>
        public SelectionState()
        {

        }

        public SelectionState(Selection selection)
        {
            Id = selection.Id;
            UpdateState(selection);
        }

        internal void UpdateState(Selection selection)
        {
            Price = selection.Price;
            Status = selection.Status;
            Tradability = selection.Tradable;
            Name = selection.Name ?? this.Name;
        }

        internal bool IsEqualTo(Selection selection)
        {
            if(selection == null)
                throw new ArgumentNullException("selection is null in SelectionState comparison");

            return (selection.Name == null || selection.Name == this.Name)
                   && this.Price == selection.Price
                   && this.Tradability == selection.Tradable
                   && this.Status == selection.Status;
        }

            
    }
}

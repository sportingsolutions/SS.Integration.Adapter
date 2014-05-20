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

namespace SS.Integration.Adapter.Model
{
    [Serializable]
    public class Selection
    {
        private readonly Dictionary<string, string> _Tags;
        private string _OverridenName;

        public Selection()
        {
            _Tags = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            _OverridenName = null;
        }

        public string Id { get; set; }

        public double? Price { get; set; }

        public string Status { get; set; }

        public bool? Tradable { get; set; }

        public string Name
        {
            get
            {
                return string.IsNullOrEmpty(_OverridenName) ? GetTagValue("name") : _OverridenName;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    _OverridenName = value;
            }
        }

        public bool IsSuspended
        {
            get
            {
                return this.Tradable.HasValue && !this.Tradable.Value;
            }
        }

        public bool? IsActive
        {
            get
            {
                if (string.IsNullOrEmpty(Status)) 
                    return null;

                return Status == SelectionStatus.Active;
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

        public bool HasTag(string tagKey)
        {
            return !string.IsNullOrEmpty(tagKey) && _Tags.ContainsKey(tagKey);
        }

        public void AddOrUpdateTagValue(string tagKey, string tagValue)
        {
            if (string.IsNullOrEmpty(tagKey))
                return;

            _Tags[tagKey] = tagValue;
        }

        public string GetTagValue(string tagKey)
        {
            return HasTag(tagKey) ? _Tags[tagKey] : null;
        }

        public int TagsCount
        {
            get
            {
                return _Tags.Count;
            }
        }

        /// <summary>
        /// Deprecated, use the API interface to deal with tags
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get
            {
                return _Tags;
            }
        }

        #endregion

        public override string ToString()
        {
            var format = "Selection with selectionId={0}";
            if (this.Name != null)
            {
                format += " selectionName=\"{1}\"";
                return string.Format(format, Id, Name);
            }

            return string.Format(format, Id);
        }
    }
}

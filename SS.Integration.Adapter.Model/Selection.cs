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
using System.Runtime.Serialization;
using SS.Integration.Common.Extensions;

namespace SS.Integration.Adapter.Model
{
    /// <summary>
    /// This class represents a selection as it comes
    /// within a delta/full snapshot.
    /// 
    /// As some information are only present in a full snapshot
    /// (i.e. tag section) not all the properties have valid
    /// value when the object is created from a delta snapshot.
    /// </summary>
    [Serializable]
    public class Selection
    {
        private readonly Dictionary<string, string> _tags;
        private string _OverridenName;

        public Selection()
        {
            _tags = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            _OverridenName = null;
        }

        /// <summary>
        /// Selection's Id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Selection's price when present
        /// within the snapshot
        /// </summary>
        public double? Price { get; set; }

        /// <summary>
        /// Selections' status. 
        /// See SelectionStatus for a list of valid values.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Selection's tradability when present
        /// within the snapshot
        /// </summary>
        public bool? Tradable { get; set; }

        /// <summary>
        /// The selection's name.
        /// 
        /// The selection's name can be overwritten by manually setting it, but
        /// it is only valid for the current object. If another snapshot arrives,
        /// the Selection object is re-created with the information contained within
        /// the snapshot.
        /// 
        /// If not manually set, this field is only valid when the object
        /// is created from a full snapshot.
        /// </summary>
        [IgnoreDataMember]
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

        /// <summary>
        /// Returns true if the selection is suspended.
        /// 
        /// This is currently computed by checking the
        /// tradability value. If the tradability is present
        /// and its value if false, then the selection is 
        /// considered as suspended.
        /// </summary>
        [IgnoreDataMember]
        public bool IsSuspended
        {
            get
            {
                return this.Tradable.HasValue && !this.Tradable.Value;
            }
        }

        [IgnoreDataMember]
        public bool? IsActive
        {
            get
            {
                if (string.IsNullOrEmpty(Status)) 
                    return null;

                return Status == SelectionStatus.Active;
            }
        }

        public Result Result { get; set; }

        public Result PlaceResult { get; set; }

        #region Tags

        [IgnoreDataMember]
        public IEnumerable<string> TagKeys
        {
            get
            {
                return _tags.Keys;
            }
        }

        public bool HasTag(string tagKey) => HasTag(tagKey, true);
        public bool HasTag(string tagKey, bool keyCaseSensitive) => !string.IsNullOrEmpty(tagKey) && _tags.FindKey(tagKey, keyCaseSensitive) != null;

        public void AddOrUpdateTagValue(string tagKey, string tagValue) => AddOrUpdateTagValue(tagKey, tagValue, true);
        public void AddOrUpdateTagValue(string tagKey, string tagValue, bool keyCaseSensitive) => _tags.AddOrUpdateValue(tagKey, tagValue, keyCaseSensitive);

        public string GetTagValue(string tagKey) => GetTagValue(tagKey, true);
        public string GetTagValue(string tagKey, bool keyCaseSensitive) => _tags.GetValue(tagKey, keyCaseSensitive);
        
        public bool IsTagValueMatch(string tagKey, string value, bool valueCaseSensitive = false, bool keyCaseSensitive = true)
            => _tags.IsValueMatch(tagKey, value, valueCaseSensitive, keyCaseSensitive);


        [IgnoreDataMember]
        public int TagsCount
        {
            get
            {
                return _tags.Count;
            }
        }

        /// <summary>
        /// Deprecated, use the API interface to deal with tags
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get
            {
                return _tags;
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

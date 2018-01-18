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
using System.Runtime.Serialization;

namespace SS.Integration.Adapter.Model
{
    /// <summary>
    /// This class represents a market as it comes within 
    /// a (delta/full)-snasphot.
    /// 
    /// As delta snapshots don't have the tag section,
    /// some properties don't have a valid value when
    /// the object is created from a delta snapshot.
    /// </summary>
    [Serializable]
    public class Market
    {
        private readonly Dictionary<string, string> _Tags;
        protected List<Selection> _selections; 


        public Market(string Id)
            : this()
        {
            this.Id = Id;
        }

        public Market()
        {
            _Tags = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            _selections = new List<Selection> ();
        }

        /// <summary>
        /// Market's name. Not present if 
        /// the snapshot is a delta snapshot
        /// </summary>

        [IgnoreDataMember]
        public string Name
        {
            get
            {
                return GetTagValue("name");
            }
        }

        /// <summary>
        /// Market's type. This information
        /// is not present if this object
        /// is coming from a delta-snapshot.
        /// </summary>
        [IgnoreDataMember]
        public string Type
        {
            get
            {
                return  GetTagValue("type");
            }
        }

        /// <summary>
        /// Market's Id
        /// </summary>
        public string Id { get; set; }

        public Rule4[] Rule4s { get; set; }

        /// <summary>
        /// Determines if the market is an 
        /// over-under market by looking a the tags.
        /// 
        /// Information not available on a delta snapshot
        /// </summary>
        [IgnoreDataMember]
        public bool IsOverUnder
        {
            get
            {
                return Selections.Any(s => s.HasTag("identifier") && string.Equals(s.GetTagValue("identifier"), "over", StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Determines if the market can be traded in play.
        /// 
        /// The information is always available
        /// </summary>
        public bool IsTradedInPlay { get; set; }

        /// <summary>
        /// Returns true if the market is active.
        /// 
        /// This information is always available and it is computed
        /// by looking a the selections' states and 
        /// theirs tradability values.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Returns true if the market is suspended.
        /// 
        /// Please note that this value only make sense
        /// when IsActive is true.
        /// 
        /// This value is always present.
        /// </summary>
        public bool IsSuspended { get; set; }

        /// <summary>
        /// Determines if the market has been
        /// resulted on the Connect platform.
        /// 
        /// The value is always present and it
        /// is computed by looking at the selections'
        /// states and theirs tradability values
        /// </summary>
        public bool IsResulted { get; set; }

        /// <summary>
        /// Determines if the market has at least
        /// one voided selection.
        /// 
        /// The value is always present and it is
        /// computed by looking at all the selections'
        /// states (included selections that are
        /// not currently contained within this
        /// object)
        /// </summary>
        public bool IsVoided { get; set; }

        /// <summary>
        /// Determines if the market is in a pending state.
        /// 
        /// This information is always present and it is
        /// computed by looking at the selections's states.
        /// </summary>
        public bool IsPending { get; set; }

        /// <summary>
        /// Returns the selections's for this market
        /// as they are contained within the snapshot.
        /// </summary>
        public virtual List<Selection> Selections
        {
            get { return _selections; }
            protected set { _selections = value; }
        }

        #region Tags

        /// <summary>
        /// Determines if the market has the given tag.
        /// 
        /// If this object has been created from a delta snapshot,
        /// this method always returns false.
        /// </summary>
        /// <param name="tagKey"></param>
        /// <returns></returns>
        public bool HasTag(string tagKey)
        {
            return !string.IsNullOrEmpty(tagKey) && _Tags.ContainsKey(tagKey);
        }

        /// <summary>
        /// Returns the value of the given tag.
        /// Null if the tag doesn't exist.
        /// </summary>
        /// <param name="tagName"></param>
        /// <returns></returns>
        public string GetTagValue(string tagName)
        {
            return HasTag(tagName) ? _Tags[tagName] : null;
        }

        /// <summary>
        /// Allows to add/update a tag
        /// </summary>
        /// <param name="tagName">Must not be empty or null</param>
        /// <param name="tagValue"></param>
        public void AddOrUpdateTagValue(string tagName, string tagValue)
        {
            if (string.IsNullOrEmpty(tagName))
                return;

            _Tags[tagName] = tagValue;
        }

        /// <summary>
        /// Returns the list of all tags
        /// </summary>
        [IgnoreDataMember]
        public IEnumerable<string> TagKeys
        {
            get
            {
                return _Tags.Keys;
            }
        }

        /// <summary>
        /// Returns the number of tags contained
        /// within this object.
        /// </summary>
        [IgnoreDataMember]
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
        [Obsolete("Use Tag interface to deal with tags", false)]
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
            string format = "Market marketId={0}";
            if (!string.IsNullOrEmpty(this.Name))
            {
                format += " marketName=\"{1}\"";
                return string.Format(format, this.Id, this.Name);
            }

            return string.Format(format, this.Id);
        }

    }
}

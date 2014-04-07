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

namespace SS.Integration.Adapter.Model
{
    [Serializable]
    public class Market
    {
        private readonly Dictionary<string, string> _Tags;

        public Market(string Id)
            : this()
        {
            this.Id = Id;
        }

        public Market()
        {
            _Tags = new Dictionary<string, string>();
            Selections = new List<Selection>();
        }

        public string Name
        {
            get
            {
                return GetTagValue("name");
            }
        }

        public string Type
        {
            get
            {
                return  GetTagValue("type");
            }
        }

        public virtual string Id { get; set; }

        public Rule4[] Rule4s { get; set; }

        public bool IsOverUnder
        {
            get
            {
                return Selections.Any(s => s.HasTag("identifier") && string.Equals(s.GetTagValue("identifier"), "over", StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool IsTradedInPlay
        {
            get { return HasTag("traded_in_play") && bool.Parse(GetTagValue("traded_in_play")); }
        }

        public bool IsActive { get; set; }

        public bool IsSuspended { get; set; }

        public bool IsResulted { get; set; }

        public bool IsPending { get; set; }

        #region Selections

        public virtual IEnumerable<Selection> Selections { get; private set; }

        public void AddSelection(Selection selection)
        {
            if (selection == null)
                return;

            ((List<Selection>)Selections).Add(selection);
        }

        #endregion

        #region Tags

        public bool HasTag(string tagKey)
        {
            return !string.IsNullOrEmpty(tagKey) && _Tags.ContainsKey(tagKey);
        }

        public string GetTagValue(string tagName)
        {
            return HasTag(tagName) ? _Tags[tagName] : null;
        }

        public void AddOrUpdateTagValue(string tagName, string tagValue)
        {
            if (string.IsNullOrEmpty(tagName))
                return;

            _Tags[tagName] = tagValue;
        }

        public IEnumerable<string> TagKeys
        {
            get
            {
                return _Tags.Keys;
            }
        }

        public int TagsCount
        {
            get
            {
                return _Tags.Count;
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

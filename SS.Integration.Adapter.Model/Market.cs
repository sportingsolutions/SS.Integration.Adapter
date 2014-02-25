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
        public Market()
        {
            Tags = new Dictionary<string, string>();
            Selections = new List<Selection>();
        }

        public virtual string Name
        {
            get
            {
                return Tags != null && Tags.ContainsKey("name") ? Tags["name"] : null;
            }
        }

        public string Type
        {
            get
            {
                return Tags != null && Tags.ContainsKey("type") ? Tags["type"] : null;
            }
        }

        public virtual string Id { get; set; }

        public Dictionary<string, string> Tags { get; private set; }

        public virtual List<Selection> Selections { get; private set; }

        public Rule4[] Rule4s { get; set; }

        public virtual bool? IsActive
        {
            get
            {
                if (Selections.All(x => string.IsNullOrEmpty(x.Status)))
                    return null;

                return Selections.Any(x => x.Status == "1");
            }
        }
        
        //Don't delete this property unless you can delete all state on customer Production. 
        public bool CanBeInserted
        {
            get { return true; }
        }

        internal bool HasTag(string tagName)
        {
            return Tags != null && Tags.ContainsKey(tagName);
        }

        public virtual bool IsOverUnder
        {
            get
            {
                return
                    this.Selections != null &&
                    this.Selections.Any(
                        x => x.Tags.ContainsKey("identifier") && x.Tags["identifier"].ToString() == "over");
            }
        }

        public virtual bool IsTradedInPlay
        {
            get { return HasTag("traded_in_play") && bool.Parse(Tags["traded_in_play"]); }
        }

        public virtual bool IsSuspended { get; set; }

        public virtual bool IsResulted
        {
            get
            {
                //If this market contains ANY selection that is settled and has a price of 1.00
                //then this is a resulted market
                // OR all selections are void
                return this.Selections.Any(x => x.Status == "2" && x.Price == 1.00) || this.Selections.All(x => x.Status == "3");
            }
        }

        public virtual bool IsPending
        {
            get
            {
                return this.Selections.All(s => s.Status == "0");
            }
        }

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

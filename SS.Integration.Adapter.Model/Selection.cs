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
        public Selection()
        {
            Tags = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);
        }

        public string Id { get; set; }

        public Dictionary<string, object> Tags { get; private set; }

        public string DisplayPrice { get; set; }

        public double? Price { get; set; }

        public string Status { get; set; }

        public bool? Tradable { get; set; }
        
        //TODO: If you need this property make it serializable otherwise adapter won't work properly
        //public Result Result { get; set; }

        //TODO: If you need this property make it serializable otherwise adapter won't work properly
        //public Result PlaceResult { get; set; }

        public string Name
        {
            get
            {
                if (Tags != null && Tags.ContainsKey("name"))
                    return Tags["name"].ToString();

                return null;
            }
            set
            {
                if (Tags != null)
                    Tags["name"] = value;
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
                if (string.IsNullOrEmpty(Status)) return null;
                return Status == "1";
            }
            set
            {
                if (!value.Value)
                {
                    Status = "0";
                }
                else
                {
                    Status = "1";
                }
            }
        }

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

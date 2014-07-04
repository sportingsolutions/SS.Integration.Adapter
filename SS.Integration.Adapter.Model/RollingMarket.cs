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
using System.Collections.ObjectModel;
using System.Linq;

namespace SS.Integration.Adapter.Model
{
    [Serializable]
    public class RollingMarket : Market
    {

        /// <summary>
        /// Returns the rolling selections's for this market
        /// as they are contained within the snapshot. 
        /// </summary>
        /// DO NOT CHANGE it to List, as List is mutable and we need to keep 
        /// both base and this object in sync
        public ReadOnlyCollection<RollingSelection> Selections { 
            get { return _selections.OfType<RollingSelection>().ToList().AsReadOnly(); }
            set { _selections = value.Cast<Selection>().ToList(); } 
        }

        public double? Line
        {
            get; set;
        }

        public RollingMarketScore Score { get; set; }
    }
}

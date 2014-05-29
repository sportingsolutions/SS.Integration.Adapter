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
using SS.Integration.Adapter.Plugin.Model.Interface;

namespace SS.Integration.Adapter.Plugin.Model
{
    [Serializable]
    public class MarketMapping : ITag
    {
        public string Type { get; set; }
        public string TemplateName { get; set; }
        public string Name { get; set; }
        public List<SelectionMapping> SelectionMappings { get; set; }
        public Dictionary<string, string> Tags { get; set; }
        public string Tag { get; set; }
        public string TagValue { get; set; }
        public string HandicapTag { get; set; }
        public bool IsHandicap { get { return HandicapMapping != null; } }
        public bool IsRollingHandicap { get; set; }
        public HandicapMapping HandicapMapping { get; set; }
        public IndexMapping IndexMapping { get; set; }
        public bool IsResulted { get; set; }
        public bool IsCorrectScore { get { return SelectionMappings != null && SelectionMappings.Any(x => x.MappedValue == "score"); } }
        public string IndexTag { get; set; }
        public string SelectionIndexTag { get; set; }
        public bool IsIndexMarket { get { return IndexMapping != null; } }
        public bool SkipResultConfirmation { get; set; }
        public bool IsLineIndicator { get; set; }

        public virtual bool CreateScorecastMarket {
            get { return !String.IsNullOrEmpty(this.ScorecastTemplateName); }
        }
        public string ScorecastTemplateName { get; set; }

        public double PriceLimit { get; set; }
        public bool IsPrimaryMarket { get; set; }
    } 

}

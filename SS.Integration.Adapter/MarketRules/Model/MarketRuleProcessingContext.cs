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

using System.Collections.Generic;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules.Model
{
    internal class MarketRuleProcessingContext : IMarketRuleProcessingContext
    {
        private readonly HashSet<Market> _DoNotRemove;
        private readonly HashSet<Market> _DoNotEdit;


        public MarketRuleProcessingContext()
        {
            _DoNotRemove = new HashSet<Market>();
            _DoNotEdit = new HashSet<Market>();
        }

        public bool CanBeRemoved(Market Market)
        {
            return Market != null && !_DoNotRemove.Contains(Market);
        }

        public bool CanBeEdited(Market Market)
        {
            return Market != null && !_DoNotEdit.Contains(Market);
        }

        public void SetAsUnEditable(Market Market)
        {
            if (Market != null)
                _DoNotEdit.Add(Market);
        }

        public void SetAsUnRemovable(Market Market)
        {
            if (Market != null)
                _DoNotRemove.Add(Market);
        }
    }
}

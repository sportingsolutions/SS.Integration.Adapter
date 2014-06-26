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

namespace SS.Integration.Adapter.Model
{
    public class MarketRuleEditIntent
    {
        public enum OperationType
        {
            REMOVE_SELECTIONS,
            CHANGE_SELECTIONS,
            ADD_SELECTIONS,
            CHANGE_DATA
        }

        public MarketRuleEditIntent(Action<Market> action, OperationType operationType)
        {
            if (action == null)
                throw new ArgumentNullException("action", "action cannot be null");

            Action = action;
            Operation = operationType;
        }

        public Action<Market> Action { get; private set; }

        public OperationType Operation { get; private set; }
    }
}

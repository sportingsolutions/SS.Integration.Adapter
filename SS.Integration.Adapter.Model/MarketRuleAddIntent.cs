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


namespace SS.Integration.Adapter.Model
{
    public class MarketRuleAddIntent
    {
        // the highest value takes priority
        public enum OperationType
        {
            CHANGE_DATA       = 100,
            ADD_SELECTIONS    = 250,
            CHANGE_SELECTIONS = 500,
            SETTLE_SELECTIONS = 1000
            
        }

        public MarketRuleAddIntent(OperationType operationType)
        {
            Operation = operationType;
        }

        public OperationType Operation { get; private set; }
    }
}

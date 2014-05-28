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

namespace SS.Integration.Adapter.Model.Interfaces
{
    public interface IMarketRule
    {
        /// <summary>
        /// The rule's name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Applies a rule. 
        /// 
        /// A rule is not allowed to 
        /// edit directly the fixture object or any of its details
        /// (markets, selections).
        /// 
        /// A rule can only specify an intent of action.
        /// A rule manager will collect together all the rules' intents
        /// and resolve theirs conflicts if any. When the the 
        /// rule manager has a set of no confliting intents
        /// it executes them.
        /// 
        /// IMarketRuleResultIntent must be returned containing
        /// all the rule's intents.
        /// 
        /// IMarketStateCollection "OldState" and "NewState" are
        /// the markets' states respectively before and after the update
        /// arrived (NewState contains the changes from the update).
        /// 
        /// Note tha OldState can be null if it is the first time
        /// a fixture is seen by the adapter.
        /// 
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="oldState"></param>
        /// <param name="newState"></param>
        /// <returns></returns>
        IMarketRuleResultIntent Apply(Fixture fixture, IMarketStateCollection oldState, IMarketStateCollection newState);
    }
}

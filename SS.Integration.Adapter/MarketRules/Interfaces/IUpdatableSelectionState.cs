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

using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules.Interfaces
{
    internal interface IUpdatableSelectionState : ISelectionState
    {
        /// <summary>
        /// Allows to update the selection's state
        /// with information coming from the Selection object
        /// 
        /// If fullSnapshot is true, then the Selection object
        /// must contain the tag section.
        /// </summary>
        /// <param name="selection"></param>
        /// <param name="fullSnapshot">True if the Selection object is coming from a full snapshot</param>
        void Update(Selection selection, bool fullSnapshot);

        /// <summary>
        /// Allows to create a deep copy of this object
        /// </summary>
        /// <returns></returns>
        IUpdatableSelectionState Clone();
    }
}

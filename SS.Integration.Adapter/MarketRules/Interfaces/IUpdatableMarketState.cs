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
    internal interface IUpdatableMarketState : IMarketState
    {
        /// <summary>
        /// Allows to update the market's state.
        /// 
        /// All the containted ISelectionState object
        /// are updated accordingly.
        /// 
        /// </summary>
        /// <param name="market"></param>
        /// <param name="fullSnapshot">True if the Market object comes from a full snapshot</param>
        void Update(Market market, bool fullSnapshot);

        /// <summary>
        /// Returns a deep-copy of this object.
        /// </summary>
        /// <returns></returns>
        IUpdatableMarketState Clone();

    }
}

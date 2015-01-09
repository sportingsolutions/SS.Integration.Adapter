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

namespace SS.Integration.Adapter.MarketRules.Interfaces
{
    public interface IUpdatableMarketStateCollection : IMarketStateCollection
    {
        /// <summary>
        /// Allows to update the markets' states with
        /// new information present within the Fixture object.
        /// 
        /// All the contained IMarketState objects are
        /// updated with the correct information.
        /// 
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="fullSnapshot">True if the Fixture object comes from a full snapshot</param>
        void Update(Fixture fixture, bool fullSnapshot);

        /// <summary>
        /// Allows to force the the suspension state on the given market.
        /// When a market is forced on a suspended state, the 
        /// IMarketState.IsForcedSuspended property will return true
        /// </summary>
        /// <param name="markets"></param>
        void OnMarketsForcedSuspension(IEnumerable<IMarketState> markets);

        /// <summary>
        /// Allows to unsuspend previously suspended market.
        /// IMarketState.IsForcedSuspended property will return false
        /// </summary>
        /// <param name="markets"></param>
        void OnMarketsForcedUnsuspension(IEnumerable<IMarketState> markets);
    }
}

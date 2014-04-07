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

namespace SS.Integration.Adapter.Model.Interfaces
{
    public interface IMarketState
    {

        string Id { get; }

        string Name { get; }

        /// <summary>
        /// Returns true if the market is in a "Suspended" state.
        /// 
        /// This property only makes sense if IsActive is true as 
        /// a "Suspended" state doesn't have any meaning when the market
        /// is not active.
        /// </summary>
        bool IsSuspended { get; }

        /// <summary>
        /// Returns true if the market is active.
        /// </summary>
        bool IsActive { get; }

        bool IsResulted { get; }

        bool IsPending { get; }

        /// <summary>
        /// True if the market has been active at least once 
        /// during the fixture lifetime
        /// </summary>
        bool HasBeenActive { get; }

        #region Selections 

        IEnumerable<ISelectionState> Selections { get; }

        ISelectionState this[string SelectionId] { get; }

        bool HasSelection(string SelectionId);

        #endregion

        #region Tags

        IEnumerable<string> TagKeys { get; }

        string GetTagValue(string TagKey);

        bool HasTag(string TagKey);

        int TagsCount { get; }

        #endregion

        void Update(Market Market, bool fullSnapshot);

        bool IsEqualTo(Market Market);

        IMarketState Clone();
    }
}

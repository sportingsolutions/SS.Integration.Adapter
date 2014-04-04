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

        bool IsSuspended { get; }

        bool IsActive { get; }

        bool IsResulted { get; }

        bool IsPending { get; }

        string Line { get; }

        /// <summary>
        /// True if the market has been active at least once 
        /// during the fixture lifetime
        /// </summary>
        bool HasBeenActive { get; }

        IEnumerable<string> TagKeys { get; }

        IEnumerable<ISelectionState> Selections { get; }

        ISelectionState this[string SelectionId] { get; }

        bool HasSelection(string SelectionId);

        string GetTagValue(string TagKey);

        void Update(Market Market);

        bool IsEqualTo(Market Market);

        IMarketState Clone();
    }
}

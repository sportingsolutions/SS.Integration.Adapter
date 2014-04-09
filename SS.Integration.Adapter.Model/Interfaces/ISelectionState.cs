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
    public interface ISelectionState
    {
        string Id { get; }

        string Status { get; }

        string Name { get; }

        bool? Tradability { get; }

        double? Price { get; }

        #region Tags

        IEnumerable<string> TagKeys { get; }

        string GetTagValue(string TagKey);

        int TagsCount { get; }

        bool HasTag(string TagKey);

        #endregion

        void Update(Selection Selection, bool fullSnapshot);

        bool IsEqualTo(Selection Selection);

        ISelectionState Clone();
    }
}

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
using SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;

namespace SS.Integration.Adapter.Diagnostics.Model.Service.Model
{
    public class FixtureDetails : FixtureOverview, IFixtureDetails
    {
        public FixtureDetails()
        {
            ProcessingEntries = new List<IFixtureProcessingEntry>();
        }

        public bool IsOver { get; set; }

        public bool IsDeleted { get; set; }

        public IEnumerable<IFixtureProcessingEntry> ProcessingEntries { get; private set; }

        public void AddProcessingEntry(IFixtureProcessingEntry entry)
        {
            if (entry == null)
                return;

            ((List<IFixtureProcessingEntry>)ProcessingEntries).Add(entry);
        }
    }
}
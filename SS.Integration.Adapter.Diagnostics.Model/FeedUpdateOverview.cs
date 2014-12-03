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

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class FeedUpdateOverview
    {
        public int Sequence { get; set; }
        public bool IsProcessed { get; set; }
        public bool IsSnapshot { get; set; }
        public DateTime Issued { get; set; }

        public string LastError { get; set; }

        /// <summary>
        /// The time it took to process the update
        /// This property will be null if the update is being processed
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }
    }
}


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

namespace SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface
{
    public enum FixtureProcessingState
    {
        PROCESSED = 0,
        PROCESSING = 1,
        SKIPPED = 2,
    }

    public interface IFixtureProcessingEntry
    {

        /// <summary>
        /// Timestamp of when the adapter received the sequence
        /// </summary>
        DateTime Timestamp { get; }

        /// <summary>
        /// Sequence number
        /// </summary>
        string Sequence { get; }

        /// <summary>
        /// Epoch contained within the update
        /// </summary>
        string Epoch { get; }

        /// <summary>
        /// EpochChangeReasons contained within the update
        /// </summary>
        int[] EpochChangeReasons { get; }

        /// <summary>
        /// False if the update is a full snapshot
        /// </summary>
        bool IsUpdate { get; }

        /// <summary>
        /// Exception that might have been raised while
        /// processing the update
        /// </summary>
        string Exception { get; }

        /// <summary>
        /// Processing state for this update
        /// </summary>
        FixtureProcessingState State { get; }
    }
}

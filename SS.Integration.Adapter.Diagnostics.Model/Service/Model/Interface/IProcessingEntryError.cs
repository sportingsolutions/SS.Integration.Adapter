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
    public interface IProcessingEntryError
    {
        /// <summary>
        /// Timestamp of when the error occured
        /// </summary>
        DateTime Timestamp { get; }

        /// <summary>
        /// The sequence at which the error occured
        /// </summary>
        int Sequence { get; }

        /// <summary>
        /// Error's message
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Id of the fixture where the error occured
        /// </summary>
        string FixtureId { get; }

        /// <summary>
        /// Description of the fixture where the error occured
        /// </summary>
        string FixtureDescription { get; }
    }
}

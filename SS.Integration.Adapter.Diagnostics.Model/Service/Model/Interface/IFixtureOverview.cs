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
    public enum FixtureState
    {
        Setup = 0,
        Ready = 1,
        PreMatch = 2,
        Running = 3,
        Over = 4,
        Deleted = 5
    }

    public interface IFixtureOverview
    {
        /// <summary>
        /// Fixture's id
        /// </summary>
        string Id { get; }

        /// <summary>
        /// True if the fixture is currently streaming
        /// </summary>
        bool IsStreaming { get; }

        /// <summary>
        /// The current state of the fixture
        /// </summary>
        FixtureState State { get; }

        /// <summary>
        /// True if the fixture is currently in
        /// an error state
        /// </summary>
        bool IsInErrorState { get; }

        /// <summary>
        /// Fixture's start time
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// SSLNCompetitionName of the fixture
        /// </summary>
        string Competition { get; }

        /// <summary>
        /// SSLNCompetitionId of the fixture
        /// </summary>
        string CompetitionId { get; }

        /// <summary>
        /// Fixture's description (ie. Manchester United v Chelsea)
        /// </summary>
        string Description { get; }

    }
}

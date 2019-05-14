﻿//Copyright 2017 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using SS.Integration.Adapter.Model.Enums;
using System;

namespace SS.Integration.Adapter.Actors.Messages
{
    public class UpdateSupervisorStateMsg
    {
        public string FixtureId { get; set; }

        public string Sport { get; set; }

        public int Epoch { get; set; }

        public int CurrentSequence { get; set; }

        public DateTime? StartTime { get; set; }

        public bool IsSnapshot { get; set; }

        public string CompetitionId { get; set; }

        public string CompetitionName { get; set; }

        public string Name { get; set; }

        public MatchStatus? MatchStatus { get; set; }

        public int[] LastEpochChangeReason { get; set; }

        public bool? IsStreaming { get; set; }

        public bool IsDeleted => MatchStatus.HasValue && MatchStatus.Value == Model.Enums.MatchStatus.Deleted;

        public bool? IsErrored { get; set; }

        public bool? IsSuspended { get; set; }

        public bool IsOver => MatchStatus.HasValue && MatchStatus.Value.IsMatchOver();

        public Exception Exception { get; set; }
    }
}

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

namespace SS.Integration.Adapter.Plugin.Model
{
    [Serializable]
    public class CompetitionMapping
    {
        public string Name { get; set; }

        public string Value { get; set; }
        
        public string MappedCompetitionId { get; set;  }

        public string TournamentName { get; set; }
        public string TournamentType { get; set; }
        public string CompetitionName { get; set;  } 
        public string CompetitionType { get; set; }
        public string Gender { get; set; }
        public string Jurisdiction { get; set; }
        public string RoundName { get; set; }

        public string SSLNCompetitionId { get; set; }
        public string SSLNCompetitionName { get; set; }

        public bool IsBlocked { get; set; }

    }
}

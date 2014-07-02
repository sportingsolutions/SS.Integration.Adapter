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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Model
{
    [Serializable]
    public class Fixture
    {
        public Fixture()
        {
            Tags = new Dictionary<string, object>();
            GameState = new Dictionary<string, object>();
            Participants = new List<Participant>();
            Markets = new List<Market>();
        }
        
        public string FixtureName { get; set; }

        public int Epoch { get; set; }

        public int[] LastEpochChangeReason { get; set; }
                
        public string Id { get; set; }
        
        public DateTime? StartTime { get; set; }
        
        public int Sequence { get; set; }
        
        public string MatchStatus { get; set; }
        
        public Dictionary<string, object> Tags { get; private set; }
        
        public Dictionary<string, object> GameState { get; private set; }

        [IgnoreDataMember]
        public List<Market> Markets { get; private set; }

        public ReadOnlyCollection<Market> StandardMarkets
        {
            get 
            {
                return Markets.Where(x => !(x is RollingMarket)).ToList().AsReadOnly();
            }
            set
            {
                Markets.AddRange(value);
            }
        }

        public ReadOnlyCollection<RollingMarket> RollingMarkets
        {
            get { return Markets.OfType<RollingMarket>().ToList().AsReadOnly();}
            set { Markets.AddRange(value); }
        }

        public List<Participant> Participants { get; private set; }

        [IgnoreDataMember]
        public bool? IsPreMatchOnly
        {
            get
            {
                if (Tags == null || !Tags.ContainsKey("PreMatchOnly"))
                    return null;

                return (bool)Tags["PreMatchOnly"];
            }
        }

        [IgnoreDataMember]
        public bool IsDeleted
        {
            get
            {
                return this.LastEpochChangeReason != null
                       && this.LastEpochChangeReason.Contains((int)EpochChangeReason.Deleted);
            }
        }

        [IgnoreDataMember]
        public bool IsStartTimeChanged
        {
            get
            {
                return this.LastEpochChangeReason != null
                       && this.LastEpochChangeReason.Contains((int)EpochChangeReason.StartTime);
            }
        }

        [IgnoreDataMember]
        public bool IsMatchStatusChanged
        {
            get
            {
                return this.LastEpochChangeReason != null
                       && this.LastEpochChangeReason.Contains((int)EpochChangeReason.MatchStatus);
            }
        }

        [IgnoreDataMember]
        public bool IsSetup
        {
            get
            {
                return int.Parse(this.MatchStatus) == (int)Enums.MatchStatus.Setup;
            }
        }

        [IgnoreDataMember]
        public bool IsPreMatch
        {
            get
            {
                return int.Parse(this.MatchStatus) == (int)Enums.MatchStatus.Prematch;
            }
        }

        [IgnoreDataMember]
        public bool IsInPlay
        {
            get
            {
                return int.Parse(this.MatchStatus) == (int)Enums.MatchStatus.InRunning;
            }
        }

        [IgnoreDataMember]
        public bool IsMatchOver
        {
            get
            {
                return int.Parse(this.MatchStatus) == (int)Enums.MatchStatus.MatchOver;
            }
        }

        public override string ToString()
        {
            var format = "Fixture with fixtureId={0} sequence={1}";
            if (this.FixtureName != null)
            {
                format += " fixtureName=\"{2}\"";
                return string.Format(format, Id, Sequence, FixtureName);
            }

            return string.Format(format, Id, Sequence);
        }

    }
}

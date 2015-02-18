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
using SS.Integration.Common;

namespace SS.Integration.Adapter.Model
{
    [Serializable]
    [DataContract]
    public class Fixture : ICloneable
    {
        public Fixture()
        {
            Tags = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);
            GameState = new Dictionary<string, object>();
            Participants = new List<Participant>();
            Markets = new List<Market>();
        }

        [DataMember]
        public string FixtureName { get; set; }
        
        [DataMember]
        public int Epoch { get; set; }

        [DataMember]
        public int[] LastEpochChangeReason { get; set; }

        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public DateTime? StartTime { get; set; }

        [DataMember]
        public int Sequence { get; set; }

        [DataMember]
        public string MatchStatus { get; set; }

        [DataMember]
        public Dictionary<string, object> Tags { get; private set; }
        
        [DataMember]
        public Dictionary<string, object> GameState { get; private set; }

        [IgnoreDataMember]
        [Reflection.Ignore]
        public List<Market> Markets { get; private set; }

        [DataMember(Name="Markets")]
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

        [DataMember]
        public ReadOnlyCollection<RollingMarket> RollingMarkets
        {
            get { return Markets.OfType<RollingMarket>().ToList().AsReadOnly();}
            set { Markets.AddRange(value); }
        }

        [DataMember]
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

        /// <summary>
        /// Timestamp indicates when update was send to a queue
        /// </summary>
        [DataMember]
        public DateTime? TimeStamp { get; set; }

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

        /// <summary>
        /// Performs a deep-clone of the object
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            return Reflection.PropertyCopy<Fixture>.CopyFrom(this);
        }
    }
}

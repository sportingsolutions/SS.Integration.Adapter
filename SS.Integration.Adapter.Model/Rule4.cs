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

namespace SS.Integration.Adapter.Model
{    
    [Serializable]
    public class Rule4
    {
        protected bool Equals(Rule4 other)
        {
            return string.Equals(ParticipantId, other.ParticipantId) && string.Equals(Type, other.Type) && Deduction == other.Deduction && string.Equals(Time, other.Time);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (ParticipantId != null ? ParticipantId.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Type != null ? Type.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Deduction;
                hashCode = (hashCode * 397) ^ (Time != null ? Time.GetHashCode() : 0);
                return hashCode;
            }
        }

        public string ParticipantId { get; set; }
        public string Type { get; set; }
        public int Deduction { get; set; }
        public string Time { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Rule4)obj);
        }
    }
}

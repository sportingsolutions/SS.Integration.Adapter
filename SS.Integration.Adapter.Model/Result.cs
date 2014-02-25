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
    public class Result : IEquatable<Result>
    {
        public int WinParticipants { get; set; }
        public int StakeParticipants { get; set; }
        public int WinPlaces { get; set; }
        public int StakePlaces { get; set; }

        public bool Equals(Result other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return WinParticipants == other.WinParticipants && StakeParticipants == other.StakeParticipants && WinPlaces == other.WinPlaces && StakePlaces == other.StakePlaces;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = WinParticipants.GetHashCode();
                hashCode = (hashCode * 397) ^ StakeParticipants.GetHashCode();
                hashCode = (hashCode * 397) ^ WinPlaces.GetHashCode();
                hashCode = (hashCode * 397) ^ StakePlaces.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(Result left, Result right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Result left, Result right)
        {
            return !Equals(left, right);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Result)obj);
        }        
    }
}

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
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Model
{
    [Serializable]
    public class FixtureState : IEquatable<FixtureState>
    {
        private int _epoch = -1;
        public string Id { get; set; }
        public int Sequence { get; set; }
        public int Epoch { get { return _epoch; } set { _epoch = value; } }
        public MatchStatus MatchStatus { get; set; }
        public string Sport { get; set; }
        public bool IsSuspendDelayedUpdate { get; set; }

        #region IEquatable

        public bool Equals(FixtureState other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return
                _epoch == other._epoch &&
                string.Equals(Id, other.Id) &&
                Sequence == other.Sequence &&
                MatchStatus == other.MatchStatus &&
                string.Equals(Sport, other.Sport);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;

            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((FixtureState)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _epoch;
                hashCode = (hashCode * 397) ^ (Id != null ? Id.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Sequence;
                hashCode = (hashCode * 397) ^ (int)MatchStatus;
                hashCode = (hashCode * 397) ^ (Sport != null ? Sport.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }
}

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
using System.Linq;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    [Serializable]
    public class ListenerOverview : IListenerOverview
    {
        private bool _hasChanged = false;
        private int? _sequence;
        private DateTime? _startTime;
        private MatchStatus? _matchStatus;
        private int[] _lastEpochChangeReason;
        private int? _epoch;
        private bool? _isStreaming;
        private bool? _isDeleted;
        private bool? _isErrored;
        private bool? _isSuspended;
        private bool? _isOver;

        public int? Sequence
        {
            get { return _sequence; }
            set
            {
                OnChanged(_sequence,value);
                _sequence = value;
            }
        }


        public int? Epoch
        {
            get { return _epoch; }
            set
            {
                OnChanged(_epoch, value);
                _epoch = value;
            }
        }

        public bool? IsStreaming
        {
            get { return _isStreaming; }
            set
            {
                OnChanged(_isStreaming, value);
                _isStreaming = value;
            }
        }

        public bool? IsDeleted
        {
            get { return _isDeleted; }
            set
            {
                OnChanged(_isDeleted, value);
                _isDeleted = value;
            }
        }

        public bool? IsErrored
        {
            get { return _isErrored; }
            set
            {
                OnChanged(_isErrored, value);
                _isErrored = value;
            }
        }
        
        public bool? IsSuspended
        {
            get { return _isSuspended; }
            set
            {
                OnChanged(_isSuspended, value);
                _isSuspended = value;
            }
        }

        public bool? IsOver
        {
            get { return _isOver; }
            set
            {
                OnChanged(_isOver, value);
                _isOver = value;
            }
        }

        public DateTime? StartTime
        {
            get { return _startTime; }
            set
            {
                OnChanged(_startTime,value);
                _startTime = value;
            }
        }
        
        public MatchStatus? MatchStatus
        {
            get { return _matchStatus; }
            set
            {
                OnChanged(_matchStatus,value);
                _matchStatus = value;
            }
        }

        public int[] LastEpochChangeReason
        {
            get { return _lastEpochChangeReason; }
            set
            {
                var areTheSame = value != null && _lastEpochChangeReason != null &&
                                 value.All(x => _lastEpochChangeReason.Contains(x));

                if (!areTheSame)
                {
                    _hasChanged = true;
                    _lastEpochChangeReason = value;
                }
            }
        }
        
        private bool HasChanged<T>(T? oldValue, T? newValue) where T:struct
        {
            //If none has a value , the value coudln't have changed
            //If old value doesn't exist the new value is the change
            if (!oldValue.HasValue || !newValue.HasValue)
                return newValue.HasValue;

            return !oldValue.Value.Equals(newValue.Value);
        }
        
        private void OnChanged<T>(T? oldValue, T? newValue) where T : struct
        {
            if(!HasChanged(oldValue,newValue))
                return;

            _hasChanged = true;
        }
        
        public ListenerOverview GetDelta()
        {
            if (_hasChanged)
            {
                _hasChanged = false;
                return this;    
            }

            return null;
        }
    }
}

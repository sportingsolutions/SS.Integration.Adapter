using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class ListenerOverview
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

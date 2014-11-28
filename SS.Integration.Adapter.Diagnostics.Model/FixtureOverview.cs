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
using System.Linq;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class FixtureOverview : IFixtureOverview
    {
        private const int MAX_AUDIT_SIZE = 10;

        private string _name;
        private int? _sequence;
        private int? _epoch;
        private bool? _isStreaming;
        private bool? _isDeleted;
        private bool? _isErrored;
        private bool? _isSuspended;
        private bool? _isOver;
        private ErrorOverview _lastError;
        private FeedUpdateOverview _feedUpdate;
        private string _competitionId;
        private string _competitionName;
        private MatchStatus? _matchStatus;
        private DateTime _timeStamp;
        private FixtureOverviewDelta _delta;
        private List<ErrorOverview> _errors = new List<ErrorOverview>(MAX_AUDIT_SIZE);
        private List<FeedUpdateOverview> _feedUpdates = new List<FeedUpdateOverview>(MAX_AUDIT_SIZE);
        private DateTime? _startTime;
        
        public FixtureOverview()
        {
            _errors = new List<ErrorOverview>(10);
        }

        protected FixtureOverviewDelta Delta
        {
            get
            {
                //returns the value of the assignment 
                return (_delta = _delta ?? new FixtureOverviewDelta() {Id = this.Id});
            }
            set
            {
                _delta = value;
                TimeStamp = DateTime.UtcNow;
            }
        }

        public string Id { get; set; }

        public int? Sequence
        {
            get { return _sequence; }
            set
            {
                OnChanged(_sequence,value,v=> Delta.Sequence = v);
                _sequence = value;
            }
        }


        public int? Epoch
        {
            get { return _epoch; }
            set
            {
                OnChanged(_epoch, value,v=> Delta.Epoch = v);
                _epoch = value;
            }
        }

        public bool? IsStreaming
        {
            get { return _isStreaming; }
            set
            {
                OnChanged(_isStreaming, value,v=> Delta.IsStreaming = v);
                _isStreaming = value;
            }
        }

        public bool? IsDeleted
        {
            get { return _isDeleted; }
            set
            {
                OnChanged(_isDeleted, value, v => Delta.IsDeleted = v);
                _isDeleted = value;
            }
        }

        public bool? IsErrored
        {
            get { return _isErrored; }
            set
            {
                OnChanged(_isErrored, value, OnErrorChanged);
                _isErrored = value;
            }
        }
        
        public bool? IsSuspended
        {
            get { return _isSuspended; }
            set
            {
                OnChanged(_isSuspended, value, v => Delta.IsSuspended = v);
                _isSuspended = value;
            }
        }

        public bool? IsOver
        {
            get { return _isOver; }
            set
            {
                OnChanged(_isOver, value, v => Delta.IsOver = v);
                _isOver = value;
            }
        }

        public DateTime? StartTime
        {
            get { return _startTime; }
            set
            {
                OnChanged(_startTime,value,v=> Delta.StartTime = v);
                _startTime = value;
            }
        }

        public ErrorOverview LastError
        {
            get { return _lastError; }
            set
            {
                UpdateError(value);
                _lastError = value; 
            }
        }

        private void OnErrorChanged(bool? isErrored)
        {
            Delta.IsErrored = isErrored;

            //Is Errored changed to false
            if (!isErrored.Value && this.LastError != null)
            {
                LastError.IsErrored = false;
                LastError.ResolvedAt = DateTime.UtcNow;

                Delta.LastError = LastError;
            }
        }

        private void UpdateError(ErrorOverview value)
        {
            _errors.Add(value);
            Delta.LastError = value;

            TrimOldItems(_errors);
        }

        private void TrimOldItems<T>(IList<T> auditList)
        {
            if(auditList.Count >= MAX_AUDIT_SIZE)   
                auditList.RemoveAt(0);
        }

        public FeedUpdateOverview FeedUpdate
        {
            get { return _feedUpdate; }
            set
            {
                FeedUpdated(value);
                _feedUpdate = value;
            }
        }

        private void FeedUpdated(FeedUpdateOverview value)
        {
            _feedUpdates.Add(value);
            Delta.FeedUpdate = value;
            TrimOldItems(_feedUpdates);
        }


        public string Name
        {
            get { return _name; }
            set
            {
                _name = value;
            }
        }

        public string Sport
        {
            get; 
            set;
        }

        public string CompetitionId
        {
            get { return _competitionId; }
            set { _competitionId = value; }
        }

        public string CompetitionName
        {
            get { return _competitionName; }
            set { _competitionName = value; }
        }

        public MatchStatus? MatchStatus
        {
            get { return _matchStatus; }
            set
            {
                OnChanged(_matchStatus, value,v => Delta.MatchStatus = v);
                _matchStatus = value;
            }
        }

        public DateTime TimeStamp
        {
            get { return _timeStamp; }
            set { _timeStamp = value; }
        }
        
        private bool HasChanged<T>(T? oldValue, T? newValue) where T:struct
        {
            //If none has a value , the value coudln't have changed
            //If old value doesn't exist the new value is the change
            if (!oldValue.HasValue || !newValue.HasValue)
                return newValue.HasValue;

            return !oldValue.Value.Equals(newValue.Value);
        }
        
        private void OnChanged<T>(T? oldValue, T? newValue, Action<T?> updateDeltaProperty) where T : struct
        {
            if(!HasChanged(oldValue,newValue))
                return;
            
            updateDeltaProperty(newValue);
        }
        
        public IEnumerable<ErrorOverview> GetErrorsAudit(int limit = 0)
        {
            if (limit == 0)
                return _errors;

            return _errors.Take(limit);
        }

        public IEnumerable<FeedUpdateOverview> GetFeedAudit(int limit = 0)
        {
            if (limit == 0)
                return _feedUpdates;

            return _feedUpdates.Take(limit);
        }
        
        
        public IFixtureOverviewDelta GetDelta()
        {
            var responseDelta = _delta;
            _delta = null;

            return responseDelta;
        }
    }
}


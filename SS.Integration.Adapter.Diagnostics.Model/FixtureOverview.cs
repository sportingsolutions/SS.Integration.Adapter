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
using System.Runtime.CompilerServices;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    [Serializable]
    public class FixtureOverview : IFixtureOverview
    {
        private const int MAX_AUDIT_SIZE = 10;

        private string _name;
        private ErrorOverview _lastError;
        private FeedUpdateOverview _feedUpdate;
        private string _competitionId;
        private string _competitionName;
        private DateTime _timeStamp;
        private FixtureOverviewDelta _delta;
        private List<ErrorOverview> _errors = new List<ErrorOverview>(MAX_AUDIT_SIZE);
        private List<FeedUpdateOverview> _feedUpdates = new List<FeedUpdateOverview>(MAX_AUDIT_SIZE);
        private IListenerOverview _listenerOverview;
        private string _id;

        /// <summary>
        /// DO NOT USE DIRECTLY IT's FOR SERIALISER ONLY
        /// </summary>
        public FixtureOverview()
        {
            _errors = new List<ErrorOverview>(10);
            _listenerOverview = new ListenerOverview();
        }

        public FixtureOverview(string fixtureId) : this()
        {
            _id = fixtureId;
            
        }

        protected FixtureOverviewDelta Delta
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                //returns the value of the assignment 
                return (_delta = _delta ?? new FixtureOverviewDelta() { Id = this.Id });
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            set
            {
                _delta = value;
                TimeStamp = DateTime.UtcNow;
            }
        }

        public string Id
        {
            get { return _id; }
            set
            {
                _id = value;
            }
        }

        
        public IListenerOverview ListenerOverview
        {
            get { return _listenerOverview; }
            set { _listenerOverview = value; }
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

        private void UpdateError(ErrorOverview value)
        {
            _errors.Add(value);
            Delta.LastError = value;

            if (Delta.FeedUpdate != null)
                Delta.FeedUpdate.LastError = Delta.LastError.Exception.Message;

            TrimOldItems(_errors);
        }

        private void TrimOldItems<T>(IList<T> auditList)
        {
            if (auditList.Count >= MAX_AUDIT_SIZE)
                auditList.RemoveAt(0);
        }

        private void GroupProcessedFeedUpdates(FeedUpdateOverview newUpdate)
        {
            if (newUpdate != null && newUpdate.IsProcessed)
            {
                var oldFeedUpdate = _feedUpdates.Find(f => f.Sequence == newUpdate.Sequence && !f.IsProcessed);
                _feedUpdates.Remove(oldFeedUpdate);
            }
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


        private void FeedUpdated(FeedUpdateOverview newFeedUpdate)
        {
            _feedUpdates.Add(newFeedUpdate);
            Delta.FeedUpdate = newFeedUpdate;
            GroupProcessedFeedUpdates(newFeedUpdate);
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
        
        public DateTime TimeStamp
        {
            get { return _timeStamp; }
            set { _timeStamp = value; }
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

        private void OnErrorChanged(IListenerOverview listenerOverview)
        {
            if (!listenerOverview.IsErrored.HasValue)
                return;

            var isErrored = listenerOverview.IsErrored.Value;

            //Is Errored changed to false
            if (!isErrored && LastError != null && LastError.IsErrored)
            {
                LastError.IsErrored = false;
                LastError.ResolvedAt = DateTime.UtcNow;

                Delta.LastError = LastError;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IFixtureOverviewDelta GetDelta()
        {
            var listererOverviewDelta = (ListenerOverview as ListenerOverview).GetDelta();
            if (_delta == null && listererOverviewDelta != null)
            {
                Delta.ListenerOverview = listererOverviewDelta;
            }

            var responseDelta = Delta;

            if (responseDelta != null)
            {
                if (responseDelta.ListenerOverview != null)
                    OnErrorChanged(responseDelta.ListenerOverview);

                //the object might exist with just Id populated but if no properties changed it should not be issued
                if (!responseDelta.HasChanged)
                    responseDelta = null;
            }
            
            _delta = null;
            


            return responseDelta;
        }
    }
}


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

namespace SS.Integration.Common.StatsAnalyser.Buckets.Properties
{
    public abstract class Property : IObservable<Property>
    {
        private class Disposer : IDisposable
        {
            private readonly List<IObserver<Property>> _Observers;
            private readonly IObserver<Property> _Observer;

            public Disposer(List<IObserver<Property>> Observers, IObserver<Property> Observer)
            {
                this._Observers = Observers;
                this._Observer = Observer;
                lock (_ObserverLock)
                {
                    _Observers.Add(_Observer);
                }
            }

            public void Dispose()
            {
                lock (_ObserverLock)
                {
                    this._Observers.Remove(_Observer);
                }
            }
        }

        private static readonly int DEFAULT_HISTORY_SIZE = 1000;
        private static readonly object _ObserverLock = new object();

        private readonly List<IObserver<Property>> _Observers;
        private List<Message> _History;
        private bool _AreNotificationsSuspended;

        protected Property(MessageBucket bucket)
        {
            Bucket = bucket;
            _Observers = new List<IObserver<Property>>();
            HistoryType = MessageBucketPropertyHistoryType.NONE;
            NumberOfHistoryItems = DEFAULT_HISTORY_SIZE;
            _AreNotificationsSuspended = false;
        }

        public MessageBucket Bucket { get; private set; }

        protected bool HasChangedSinceLastNofitication { get; set; }

        public MessageBucketPropertyHistoryType HistoryType { get; private set; }

        public int NumberOfHistoryItems { get; private set; }

        public Property SetHistoryNumberOfItems(int nitems)
        {
            NumberOfHistoryItems = nitems;
            return this;
        }

        public Property SetHistoryType(MessageBucketPropertyHistoryType messageBucketPropertyHistoryType)
        {
            HistoryType = messageBucketPropertyHistoryType;
            switch (messageBucketPropertyHistoryType)
            {
                case MessageBucketPropertyHistoryType.FROM_START:
                case MessageBucketPropertyHistoryType.ONLY_NEW:
                    _History = new List<Message>();
                    break;
            }

            return this;
        }

        internal abstract void SetValue(Message message, bool newmessage);

        protected void AddHistoryItem(Message message, bool newmessage)
        {
            if (message == null)
                return;

            if (HistoryType == MessageBucketPropertyHistoryType.FROM_START ||
               (HistoryType == MessageBucketPropertyHistoryType.ONLY_NEW && newmessage))
            {
                lock (this)
                {
                    _History.Add(message);
                    if (_History.Count > NumberOfHistoryItems)
                    {
                        while (_History.Count > NumberOfHistoryItems)
                        {
                            _History.RemoveAt(0);
                        }
                    }
                }
            }
        }

        public IEnumerable<Message> GetHistoryValues(Message lastseenmessage = null)
        {
            if (_History == null)
                return null;

            List<Message> copy = new List<Message>();
            lock (this)
            {
                int index = _History.IndexOf(lastseenmessage);

                for (int i = index + 1; i < _History.Count; i++)
                {
                    copy.Add(_History[i]);
                }
            }

            return copy.AsEnumerable();
        }

        public IDisposable Subscribe(IObserver<Property> observer)
        {
            if (observer == null)
                return null;

            Disposer disposer = new Disposer(_Observers, observer);
            return disposer;            
        }

        protected void NotifyObservers()
        {
            if (_AreNotificationsSuspended)
                return;

            IObserver<Property>[] observers = null;
            lock (_Observers)
            {
                observers = new IObserver<Property>[_Observers.Count];
                _Observers.CopyTo(observers);
            }

            foreach (var obj in observers)
                obj.OnNext(this);
        }

        internal void ResumeNotifications(bool pending)
        {
            _AreNotificationsSuspended = false;
            if (pending && HasChangedSinceLastNofitication)
            {
                HasChangedSinceLastNofitication = false;
                NotifyObservers();
            }
        }

        internal void SuspendNotifications()
        {
            _AreNotificationsSuspended = true;
        }
    }
}

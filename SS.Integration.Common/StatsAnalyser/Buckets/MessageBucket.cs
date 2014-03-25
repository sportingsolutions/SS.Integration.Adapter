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

using SS.Integration.Common.StatsAnalyser.Buckets.Properties;
using System;
using System.Collections.Generic;

namespace SS.Integration.Common.StatsAnalyser.Buckets
{
    public class MessageBucket : IObservable<MessageBucket>
    {
        private class Disposer : IDisposable
        {
            private readonly List<IObserver<MessageBucket>> _Observers;
            private readonly IObserver<MessageBucket> _Observer;

            public Disposer(List<IObserver<MessageBucket>> Observers, IObserver<MessageBucket> Observer)
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


        private readonly List<IObserver<MessageBucket>> _Observers;
        private readonly Dictionary<string, Property> _Properties;
        private bool _AreNotificationsSuspended;
        private bool _HasChangedSinceLastNofication;
        private static readonly object _ObserverLock = new object();

        public MessageBucket(string source)
        {
            Source = source;
            _Properties = new Dictionary<string, Property>();
            _Observers = new List<IObserver<MessageBucket>>();
            _AreNotificationsSuspended = false;
            _HasChangedSinceLastNofication = false;
            LastChange = DateTime.Now;
        }

        public string Source { get; private set; }

        public IEnumerable<Property> Properties
        {
            get 
            {
                return _Properties.Values;
            }
        }

        public DateTime LastChange { get; private set; }

        public Property ConfigureProperty(string key, MessageBucketPropertyType messageBucketType = MessageBucketPropertyType.SINGLE_VALUE)
        {
            Property property = null;
            switch (messageBucketType)
            {
                case MessageBucketPropertyType.LIST:
                    property = new ListableProperty(this);
                    break;
                case MessageBucketPropertyType.SINGLE_VALUE:
                    property = new SingleValueProperty(this);
                    break;
                default:
                    throw new Exception("Unknown property type");
            }

            _Properties[key] = property;
            return property;
        }

        private Property ConfigurePropertyInternal(string key, string id, MessageBucketPropertyType type, MessageBucketPropertyHistoryType history, int nitems)
        {
            Property property = null;
            switch (type)
            {
                case MessageBucketPropertyType.LIST:
                    property = new ListableProperty(this);
                    break;
                case MessageBucketPropertyType.SINGLE_VALUE:
                    property = new SingleValueProperty(this);
                    break;
                default:
                    throw new Exception("Unknown property type");
            }

            property.SetHistoryType(history).SetHistoryNumberOfItems(nitems);

            string realkey = key + "-" + id;
            _Properties[realkey] = property;
            return property;
        }

        public Property GetProperty(string key, string id = "")
        {
            string realkey = key + "-" + id;

            if (_Properties.ContainsKey(realkey))
                return _Properties[realkey];

            if (_Properties.ContainsKey(key))
            {
                Property original = _Properties[key];

                if (string.IsNullOrEmpty(id))
                    return original;

                MessageBucketPropertyType type = MessageBucketPropertyType.SINGLE_VALUE;
                if(original is ListableProperty)
                    type = MessageBucketPropertyType.LIST;

                return ConfigurePropertyInternal(key, id, type, original.HistoryType, original.NumberOfHistoryItems);
            }
                
            return null;
        }

        internal void AddMessage(Message message, bool newmessage)
        {
            if (message == null)
                return;

            Property property = GetProperty(message.Key, message.Id);
            if (property == null)
                return;            

            property.SetValue(message, newmessage);
            _HasChangedSinceLastNofication = true;
            LastChange = DateTime.Now;
            NotifyObservers();
        }

        public IDisposable Subscribe(IObserver<MessageBucket> observer)
        {
            if (observer == null)
                return null;

            Disposer disposer = new Disposer(_Observers, observer);
            return disposer;      
        }

        private void NotifyObservers()
        {
            if (_AreNotificationsSuspended)
                return;

            IObserver<MessageBucket>[] observers = null;
            lock (_Observers)
            {
                observers = new IObserver<MessageBucket>[_Observers.Count];
                _Observers.CopyTo(observers);
            }

            foreach (IObserver<MessageBucket> observer in observers)
                observer.OnNext(this);
        }

        internal void SuspendNotifications()
        {
            _AreNotificationsSuspended = true;

            foreach (Property property in _Properties.Values)
                property.SuspendNotifications();
        }

        internal void ResumeNotifications(bool pending)
        {
            _AreNotificationsSuspended = false;

            foreach (Property property in _Properties.Values)
                property.ResumeNotifications(pending);

            if (pending && _HasChangedSinceLastNofication)
            {
                _HasChangedSinceLastNofication = false;
                NotifyObservers();
            }
        }
    }
}

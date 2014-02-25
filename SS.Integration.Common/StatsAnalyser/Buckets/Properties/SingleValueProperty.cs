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


namespace SS.Integration.Common.StatsAnalyser.Buckets.Properties
{
    public class SingleValueProperty : Property
    {
        private Message _CurrentValue;

        internal SingleValueProperty(MessageBucket bucket) 
            : base(bucket) { }

        internal override void SetValue(Message message, bool newmessage)
        {
            Message prev = null;
            lock (this)
            {
                prev = _CurrentValue;
                _CurrentValue = message;
            }

            HasChangedSinceLastNofitication = true;
            AddHistoryItem(prev, newmessage);
            NotifyObservers();
        }

        public Message Value
        {
            get
            {
                Message msg = null;
                lock (this)
                {
                    msg = _CurrentValue;
                }

                return msg;
            }
        }
    }
}

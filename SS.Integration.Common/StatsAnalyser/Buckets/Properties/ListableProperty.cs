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
using System.Collections.Concurrent;
using System.Collections.Generic;
using SS.Integration.Common.Stats.Keys;

namespace SS.Integration.Common.StatsAnalyser.Buckets.Properties
{
    public class ListableProperty : Property
    {
        private readonly ConcurrentDictionary<string, Message> _Values;

        internal ListableProperty(MessageBucket bucket)
            : base(bucket)
        {
            _Values = new ConcurrentDictionary<string, Message>();            
        }


        internal override void SetValue(Message message, bool newmessage)
        {   
            if (message.HasMesssage(GlobalKeys.ADD_ITEM))
            {
                if (_Values.TryAdd(message.Value, message))
                {
                    HasChangedSinceLastNofitication = true;
                    LastChange = DateTime.Now;
                    NotifyObservers();
                }
            }
            else if (message.HasMesssage(GlobalKeys.REMOVE_ITEM))
            {
                Message val;
                if (_Values.TryRemove(message.Value, out val))
                {
                    HasChangedSinceLastNofitication = true;
                    AddHistoryItem(message, newmessage);
                    LastChange = DateTime.Now;
                    NotifyObservers();
                }
            }
        }

        public IEnumerable<Message> Values
        {
            get
            {
                var enumerator = _Values.GetEnumerator();
                while (enumerator.MoveNext())
                    yield return enumerator.Current.Value;
            }
        }
    }
}

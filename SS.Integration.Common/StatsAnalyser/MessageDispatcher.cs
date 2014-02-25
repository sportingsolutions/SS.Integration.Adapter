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

using System.Collections.Generic;
using SS.Integration.Common.StatsAnalyser.Buckets;

namespace SS.Integration.Common.StatsAnalyser
{
    class MessageDispatcher
    {
        private readonly Dictionary<string, MessageBucket> _Buckets;        

        internal MessageDispatcher()
        {
            _Buckets = new Dictionary<string, MessageBucket>();            
        }

        internal MessageBucket RegisterBucket(string source)
        {
            if (!_Buckets.ContainsKey(source))
                _Buckets.Add(source, new MessageBucket(source));

            return _Buckets[source];
        }

        internal void SuspendNotifications()
        {
            foreach (MessageBucket bucket in _Buckets.Values)
            {
                bucket.SuspendNotifications();
            }
        }

        internal void ResumeNotifications(bool pending)
        {
            foreach (MessageBucket bucket in _Buckets.Values)
            {
                bucket.ResumeNotifications(pending);
            }
        }

        internal void DispatchMessage(Message message, bool newmessage)
        {
            if(message == null)
                return;

            MessageBucket bucket = GetBucket(message.Source);
            if (bucket != null)
                bucket.AddMessage(message, newmessage);
        }

        internal MessageBucket GetBucket(string sourcename)
        {
            return _Buckets.ContainsKey(sourcename) ? _Buckets[sourcename] : null;
        }
    }
}

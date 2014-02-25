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

namespace SS.Integration.Common.StatsAnalyser
{
    public class Message
    {
        private class MessageData
        {
            public string Id { get; set; }
            public string S { get; set; }
            public string K { get; set; }
            public string V { get; set; }
            public Dictionary<string,string> M { get; set; }
        }

        private Message () {}

        public DateTime Date { get; private set; }

        private MessageData Data { get; set;}

        internal string Source { get { return Data.S; } }

        internal string Key { get { return Data.K; } }

        public string Value { get { return Data.V; } }

        public string Id { get { return Data.Id; } }

        public bool HasMesssage(string messagekey)
        {
            return Data.M.ContainsKey(messagekey);
        }

        public string GetMessage(string messagekey)
        {
            if (HasMesssage(messagekey))
                return Data.M[messagekey];
            return "";
        }

        public IEnumerable<string> MessageKeys
        {
            get 
            {
                return Data.M.Keys;
            }
        }
    }
}

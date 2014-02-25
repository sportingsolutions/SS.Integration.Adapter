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

using log4net;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Dynamic;

namespace SS.Integration.Common.Stats
{
    internal class StatsLogger
    {
        private readonly ILog _Logger;
        private readonly string _Name;

        public StatsLogger(ILog log, string name)
        {
            _Name = name;
            _Logger = log;
        }

        public void Write(string handleid, string key, string value, Dictionary<string, string> messages)
        {
            dynamic logmessage = new ExpandoObject();
            logmessage.id = handleid;
            logmessage.s = _Name;
            logmessage.k = key;
            logmessage.v = value;
            logmessage.m = messages;
            _Logger.Info(JsonConvert.SerializeObject(logmessage, Formatting.None));
        }
    }
}

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
using System.Collections.Generic;
using SS.Integration.Common.Stats.Interface;

namespace SS.Integration.Common.Stats
{
    public class StatsManager
    {
        private static readonly ILog _Logger = LogManager.GetLogger("StatsManager");
        private static StatsManager _Instance;
        private readonly string _Name;
        private readonly Dictionary<string, StatsManager> _Managers;
        private readonly Dictionary<string, IStatsHandle> _Handles;
        private readonly StatsLogger _LogWrapper;


        private StatsManager(string name)
        {
            _Name = name;
            _Managers = new Dictionary<string, StatsManager>();
            _LogWrapper = new StatsLogger(_Logger, name);
            _Handles = new Dictionary<string, IStatsHandle>();
        }

        public static StatsManager Instance
        {
            get { return _Instance ?? (_Instance = new StatsManager("Root")); }
        }

        public string Name { get { return _Name; } }

        public StatsManager this[string name] 
        {
            get{

                if(string.IsNullOrEmpty(name))
                    return this;

                if (!_Managers.ContainsKey(name))
                    _Managers.Add(name, new StatsManager(name));
    
                return _Managers[name];                
            }
        }

        public IStatsHandle GetHandle()
        {
            return GetHandle("");
        }

        public IStatsHandle GetHandle(string id)
        {
            if (id == null)
                id = "";

            if (!_Handles.ContainsKey(id))
                _Handles.Add(id, new StatsHandle(id, _LogWrapper));

            return _Handles[id];
        }
    }
}

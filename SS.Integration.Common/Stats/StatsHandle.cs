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

using SS.Integration.Common.Stats.Interface;

namespace SS.Integration.Common.Stats
{
    internal class StatsHandle : IStatsHandle
    {

        internal StatsHandle(StatsManager manager)
        {
            Manager = manager;
        }


        public StatsManager Manager { get; private set; }

        public void SetValue(string key, string value)
        {
            try
            {
                SetValueUnsafe(key, value);
            }
            catch { }
        }

        public void SetValueUnsafe(string key, string value)
        {
            foreach (var consumer in Manager.Consumers)
            {
                consumer.SetValue(string.Concat(Manager.Code, ".", key), value);
            }
        }

        public void IncrementValue(string key)
        {
            try
            {
                IncrementValueUnsafe(key);
            }
            catch { }
        }

        public void IncrementValueUnsafe(string key)
        {
            foreach (var consumer in Manager.Consumers)
            {
                consumer.IncrementValue(string.Concat(Manager.Code, ".", key));
            }
        }

        public void AddValue(string key, string value)
        {
            
            try
            {
                AddValueUnsafe(key, value);
            }
            catch { }
        }

        public void AddValueUnsafe(string key, string value)
        {
            foreach (var consumer in Manager.Consumers)
            {
                consumer.AddValue(string.Concat(Manager.Code, ".", key), value);
            }
        }
    }
}

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
using SS.Integration.Common.Stats.Interface;
using SS.Integration.Common.Stats.Keys;

namespace SS.Integration.Common.Stats
{
    internal class StatsHandle : IStatsHandle
    {
        private readonly StatsLogger _Logger;
        private readonly string _Id;
        private readonly Dictionary<object, int> _Increments;
        private readonly Dictionary<string, string> _Messages;

        public StatsHandle(string id, StatsLogger Logger)
        {
            _Increments = new Dictionary<object, int>();
            _Messages = new Dictionary<string, string>();
            _Logger = Logger;
            _Id = id;
        }

        public void SetValue(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (value == null)
                value = "";
            try
            {
                _Logger.Write(_Id, key, value.ToString(), _Messages);
            }
            catch { }
            finally
            {
                _Messages.Clear();
            }
        }

        public IStatsHandle AddMessage(string messagekey, object value)
        {
            if (messagekey == null)
                return this;

            if (value == null)
                value = "";

            _Messages[messagekey] = value.ToString();
            return this;
        }

        public void IncrementValue(string key)
        {
            if (key == null)
                return;

            string value;
            try
            {
                lock (this)
                {
                    if (!_Increments.ContainsKey(key))
                        _Increments.Add(key, 0);

                    _Increments[key]++;
                    value = _Increments[key].ToString();
                }

                _Logger.Write(_Id, key, value, _Messages);
            }
            catch { }
            finally
            {
                _Messages.Clear();
            }
        }

        public void DecrementValue(string key)
        {
            if (key == null)
                return;

            try
            {
                string value;
                lock (this)
                {
                    if (!_Increments.ContainsKey(key))
                        _Increments.Add(key, 0);

                    _Increments[key]--;
                    value = _Increments[key].ToString();
                }

                _Logger.Write(_Id, key, value, _Messages);
            }
            catch { }
            finally
            {
                _Messages.Clear();
            }
        }

        public void RemoveValue(string key, object value)
        {
            if (value == null || key == null)
                return;

            try
            {
                _Messages.Add(GlobalKeys.REMOVE_ITEM, "");
                _Logger.Write(_Id, key, value.ToString(), _Messages);
            }
            catch { }
            finally
            {
                _Messages.Clear();
            }
        }

        public void AddValue(string key, object value)
        {
            if (value == null || key == null)
                return;

            try
            {
                _Messages.Add(GlobalKeys.ADD_ITEM, "");
                _Logger.Write(_Id, key, value.ToString(), _Messages);
            }
            catch { }
            finally
            {
                _Messages.Clear();
            }
        }
    }
}

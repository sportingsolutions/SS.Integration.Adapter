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
using log4net;
using SS.Integration.Common.Stats.Interface;

namespace SS.Integration.Common.Stats.Consumer
{
    public class LoggerConsumer : IStatsConsumer
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(LoggerConsumer));
        private readonly Dictionary<string, int> _counters;
        private readonly Dictionary<string, string> _settings;

        private string _loggingLevel;

        public LoggerConsumer()
        {
            _settings = new Dictionary<string, string>();
            _counters = new Dictionary<string,int>();
            _loggingLevel = "INFO";
        }

        public string Name { get; set; }

        public bool IsEnabled { get; set; }

        public void AddSettingProperty(string key, string value) 
        {
            if (!string.IsNullOrEmpty(key))
                _settings[key] = value;
        }

        public string GetSettingProperty(string key)
        {
            return !string.IsNullOrEmpty(key) || !_settings.ContainsKey(key) ? null : _settings[key];
        }

        public void AddValue(string key, string value)
        {
            WriteValue(key, value);
        }

        public void SetValue(string key, string value)
        {
            WriteValue(key, value);
        }

        public void IncrementValue(string key)
        {
            if(!_counters.ContainsKey(key))
                _counters[key] = -1;

            _counters[key] = _counters[key] + 1;
            WriteValue(key, _counters[key].ToString());
        }

        private void WriteValue(string key, string value)
        {
            switch (_loggingLevel)
            {
                case "INFO":
                    _logger.Info(string.Concat(key, ": ", value));
                    break;
                case "DEBUG":
                    _logger.Debug(string.Concat(key, ": ", value));
                    break;
                case "WARN":
                    _logger.Warn(string.Concat(key, ": ", value));
                    break;
                case "ERROR":
                    _logger.Error(string.Concat(key, ": ", value));
                    break;
            }
        }

        public void Configure()
        {
            var level = GetSettingProperty("loggingLevel");
            if (string.IsNullOrEmpty(level))
                return;

            _loggingLevel = level;
        }
    }
}

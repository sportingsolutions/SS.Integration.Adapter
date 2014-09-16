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
using SS.Integration.Common.Stats.Interface;
using StatsdClient;

namespace SS.Integration.Adapter.Statsd
{
    public class StatsdConsumer : IStatsConsumer
    {
        private readonly Dictionary<string, string> _settings;
        private const string DEFAULT_PREFIX = "integration.";

        public StatsdConsumer()
        {
            _settings = new Dictionary<string, string>();
        }

        public string Name { get; set; }

        public bool IsEnabled { get; set; }

        public void Configure()
        {
            var url = GetSettingProperty("url");
            if (string.IsNullOrEmpty(url))
            {
                IsEnabled = false;
                return;
            }
            var env = GetSettingProperty("environment");
            if(string.IsNullOrEmpty(env))
            {
                IsEnabled = false;
                return;
            }

            var host = GetSettingProperty("host");
            if(string.IsNullOrEmpty(host))
            {
                host = Environment.MachineName;
            }

            var key = string.Concat(DEFAULT_PREFIX, env, ".", host, ".");

            var metricsConfig = new MetricsConfig
            {
                StatsdServerName = url,
                Prefix = key
            };

            Metrics.Configure(metricsConfig);
        }

        public void AddSettingProperty(string key, string value)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                return;

            _settings[key] = value;
        }

        public string GetSettingProperty(string key)
        {
            return string.IsNullOrEmpty(key) || !_settings.ContainsKey(key) ? null : _settings[key];
        }

        public void AddValue(string key, string value)
        {
            int dummy;
            if (Int32.TryParse(value, out dummy))
                Metrics.Timer(key, dummy);
        }

        public void SetValue(string key, string value)
        {
            double dummy;
            if (Double.TryParse(value, out dummy))
                Metrics.Gauge(key, dummy);
        }

        public void IncrementValue(string key)
        {
            Metrics.Counter(key);
        }
    }
}

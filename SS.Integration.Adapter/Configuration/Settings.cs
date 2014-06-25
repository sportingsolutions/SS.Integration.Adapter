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
using System.Configuration;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Configuration
{
    public class Settings : ISettings
    {
        private const int DEFAULT_FIXTURE_CHECK_FREQUENCY_VALUE = 60000;
        private const int DEFAULT_FIXTURE_CREATION_CONCURRENCY_VALUE = 20;
        private const int DEFAULT_STARTING_RETRY_DELAY_VALUE = 500;
        private const int DEFAULT_MAX_RETRY_DELAY_VALUE = 65000;
        private const int DEFAULT_MAX_RETRY_ATTEMPT_VALUE = 3;
        private const int DEFAULT_ECHO_INTERVAL_VALUE = 10000;
        private const int DEFAULT_ECHO_DELAY_VALUE = 3000;
        private const bool DEFAULT_SUSPEND_ALL_MARKETS_ON_SHUTDOWN_VALUE = true;
        private const int DEFAULT_HEARTBEAT_INTERVAL_VALUE = 60;
        private const string DEFAULT_EVENT_STATE_FILE_PATH_VALUE = @"C:\eventState.json";
        private const string DEFAULT_MARKET_STATE_MANAGER_DIRECTORY = @"MarketsState";
        private const int DEFAULT_CACHE_EXPIRY_MINUTES_VALUE = 15;
        private const bool DEFAULT_ENABLE_DELTA_RULE = true;


        public Settings()
        {
            User = ConfigurationManager.AppSettings["user"];
            Password = ConfigurationManager.AppSettings["password"];
            Url = ConfigurationManager.AppSettings["url"];


            var value = ConfigurationManager.AppSettings["newFixtureCheckerFrequency"];
            FixtureCheckerFrequency = string.IsNullOrEmpty(value) ? DEFAULT_FIXTURE_CHECK_FREQUENCY_VALUE : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["fixtureCreationConcurrency"];
            FixtureCreationConcurrency = string.IsNullOrEmpty(value) ? DEFAULT_FIXTURE_CREATION_CONCURRENCY_VALUE : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["startingRetryDelay"];
            StartingRetryDelay = string.IsNullOrEmpty(value) ? DEFAULT_STARTING_RETRY_DELAY_VALUE : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["maxRetryDelay"];
            MaxRetryDelay = string.IsNullOrEmpty(value) ? DEFAULT_MAX_RETRY_DELAY_VALUE : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["maxRetryAttempts"];
            MaxRetryAttempts = string.IsNullOrEmpty(value) ? DEFAULT_MAX_RETRY_ATTEMPT_VALUE : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["echoInterval"];
            EchoInterval = string.IsNullOrEmpty(value) ? DEFAULT_ECHO_INTERVAL_VALUE : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["echoDelay"];
            EchoDelay = string.IsNullOrEmpty(value) ? DEFAULT_ECHO_DELAY_VALUE : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["suspendAllOnShutdown"];
            SuspendAllMarketsOnShutdown = string.IsNullOrEmpty(value) ? DEFAULT_SUSPEND_ALL_MARKETS_ON_SHUTDOWN_VALUE : string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

            value = ConfigurationManager.AppSettings["heartBeatIntervalSeconds"];
            HeartBeatIntervalSeconds = string.IsNullOrEmpty(value) ? DEFAULT_HEARTBEAT_INTERVAL_VALUE : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["eventStateFilePath"];
            EventStateFilePath = string.IsNullOrEmpty(value) ? DEFAULT_EVENT_STATE_FILE_PATH_VALUE : Convert.ToString(value);

            value = ConfigurationManager.AppSettings["marketFiltersDirectory"];
            MarketFiltersDirectory = string.IsNullOrEmpty(value) ? DEFAULT_MARKET_STATE_MANAGER_DIRECTORY : Convert.ToString(value);

            value = ConfigurationManager.AppSettings["cacheExpiryInMins"];
            CacheExpiryInMins = string.IsNullOrEmpty(value) ? DEFAULT_CACHE_EXPIRY_MINUTES_VALUE : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["deltaRuleEnabled"];
            DeltaRuleEnabled = string.IsNullOrEmpty(value) ? DEFAULT_ENABLE_DELTA_RULE : Convert.ToBoolean(value);
        }

        public string MarketFiltersDirectory { get; private set; }

        public int CacheExpiryInMins { get; private set; }

        public string User { get; private set; }

        public string Password { get; private set; }

        public string Url { get; private set; }

        public int FixtureCheckerFrequency { get; private set; }

        public int StartingRetryDelay { get; private set; }

        public int MaxRetryDelay { get; private set; }

        public int MaxRetryAttempts { get; private set; }

        public int EchoInterval { get; private set; }

        public int EchoDelay { get; private set; }

        public bool SuspendAllMarketsOnShutdown { get; private set; }

        public int HeartBeatIntervalSeconds { get; private set; }

        public string EventStateFilePath { get; private set; }

        public int FixtureCreationConcurrency { get; set; }

        public bool DeltaRuleEnabled { get; set; }

    }
}

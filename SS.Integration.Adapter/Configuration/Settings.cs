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
using System.Text;
using log4net;
using log4net.Repository.Hierarchy;
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
        private const string DEFAULT_EVENT_STATE_FILE_PATH_VALUE = @"C:\eventState.json";
        private const string DEFAULT_MARKET_STATE_MANAGER_DIRECTORY = @"MarketsState";
        private const int DEFAULT_CACHE_EXPIRY_MINUTES_VALUE = 15;
        private const bool DEFAULT_ENABLE_DELTA_RULE = false;
        private const bool DEFAULT_USE_STATS = false;
        private const bool DEFAULT_USE_SUPERVISOR = false;
        private const int DEFAULT_PROCESSING_LOCK_TIMEOUT = 720;
        private const string DEFAULT_STOP_STREAMING_DELAYED_SPORTS = "";
        private const double DEFAULT_STOP_STREAMING_DELAY_MINUTES = 0;
        private const string DEFAULT_SUPERVISOR_STATE_PATH = @"SupervisorState";
        private const int DEFAULT_PREMATCH_SUSPENSION_BEFORE_STARTTIME_IN_MINS = 15;
        private const int DEFAULT_START_STREAMING_TIMEOUT = 60;
        private const int DEFAULT_STREAM_THRESHOLD = int.MaxValue;
        private const bool DEFAULT_ALLOW_FIXTURE_STREAMING_IN_SETUP_MODE = false;

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

            value = ConfigurationManager.AppSettings["eventStateFilePath"];
            EventStateFilePath = string.IsNullOrEmpty(value) ? DEFAULT_EVENT_STATE_FILE_PATH_VALUE : Convert.ToString(value);

            value = ConfigurationManager.AppSettings["marketFiltersDirectory"];
            MarketFiltersDirectory = string.IsNullOrEmpty(value) ? DEFAULT_MARKET_STATE_MANAGER_DIRECTORY : Convert.ToString(value);

            value = ConfigurationManager.AppSettings["cacheExpiryInMins"];
            CacheExpiryInMins = string.IsNullOrEmpty(value) ? DEFAULT_CACHE_EXPIRY_MINUTES_VALUE : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["deltaRuleEnabled"];
            DeltaRuleEnabled = string.IsNullOrEmpty(value) ? DEFAULT_ENABLE_DELTA_RULE : Convert.ToBoolean(value);

            value = ConfigurationManager.AppSettings["statsEnabled"];
            StatsEnabled = string.IsNullOrEmpty(value) ? DEFAULT_USE_STATS : Convert.ToBoolean(value);

            value = ConfigurationManager.AppSettings["useSupervisor"];
            UseSupervisor = string.IsNullOrEmpty(value) ? DEFAULT_USE_SUPERVISOR : Convert.ToBoolean(value);

            value = ConfigurationManager.AppSettings["processingLockTimeOutInSecs"];
            ProcessingLockTimeOutInSecs = string.IsNullOrEmpty(value) ? DEFAULT_PROCESSING_LOCK_TIMEOUT : Convert.ToInt32(value);
            value = ConfigurationManager.AppSettings["stopStreamingDelayMinutes"];
            StopStreamingDelayMinutes = string.IsNullOrEmpty(value) ? DEFAULT_STOP_STREAMING_DELAY_MINUTES : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["supervisorStatePath"];
            SupervisorStatePath = string.IsNullOrEmpty(value) ? DEFAULT_SUPERVISOR_STATE_PATH : value;
            value = ConfigurationManager.AppSettings["stopStreamingDelayedSports"];
            StopStreamingDelayedSports = string.IsNullOrEmpty(value) ? DEFAULT_STOP_STREAMING_DELAYED_SPORTS : value;

            value = ConfigurationManager.AppSettings["preMatchSuspensionBeforeStartTimeInMins"];
            PreMatchSuspensionBeforeStartTimeInMins = string.IsNullOrEmpty(value)
                ? DEFAULT_PREMATCH_SUSPENSION_BEFORE_STARTTIME_IN_MINS
                : int.Parse(value);

            value = ConfigurationManager.AppSettings["startStreamingTimeoutInSeconds"];
            StartStreamingTimeoutInSeconds = string.IsNullOrEmpty(value)
                ? DEFAULT_START_STREAMING_TIMEOUT
                : int.Parse(value);

            value = ConfigurationManager.AppSettings["disablePrematchSuspensionOnDisconnection"];
            DisablePrematchSuspensionOnDisconnection = string.IsNullOrEmpty(value) ? false : Convert.ToBoolean(value);

            value = ConfigurationManager.AppSettings["skipRulesOnError"];
            SkipRulesOnError = string.IsNullOrEmpty(value) ? false : Convert.ToBoolean(value);

            value = ConfigurationManager.AppSettings["streamSafetyThreshold"];
            StreamSafetyThreshold = string.IsNullOrEmpty(value) ? DEFAULT_STREAM_THRESHOLD : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["allowFixtureStreamingInSetupMode"];
            AllowFixtureStreamingInSetupMode = !string.IsNullOrEmpty(value) && Convert.ToBoolean(value);

            LogAll();
        }

        private void LogAll()
        {
            var properties = this.GetType().GetProperties();
            var logString = new StringBuilder();
            foreach (var propertyInfo in properties)
            {
                logString.AppendLine($"Setting {propertyInfo.Name}={propertyInfo.GetValue(this)}");
            }

            //it's not defined at the class level because it's only used once during lifetime of the class
            var logger = LogManager.GetLogger(typeof(Settings));
            logger.Info(logString.ToString());
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

        [Obsolete]
        public string EventStateFilePath { get; private set; }

        public bool DeltaRuleEnabled { get; private set; }

        public int FixtureCreationConcurrency { get; private set; }

        public bool StatsEnabled { get; private set; }

        public string StateProviderPath { get; private set; }

        public int ProcessingLockTimeOutInSecs { get; private set; }
        public double StopStreamingDelayMinutes { get; private set; }

        public string SupervisorStatePath { get; private set; }
        public string StopStreamingDelayedSports { get; private set; }
        public bool DisablePrematchSuspensionOnDisconnection { get; private set; }
        public int PreMatchSuspensionBeforeStartTimeInMins { get; private set; }
        public int StartStreamingTimeoutInSeconds { get; private set; }
        public bool AllowFixtureStreamingInSetupMode { get; }
        public bool SkipRulesOnError { get; private set; }
        public int StreamSafetyThreshold { get; private set; }

        public bool UseSupervisor { get; private set; }
        public bool ShouldDelayStopStreaming(string sport)
        {
            return string.IsNullOrEmpty(StopStreamingDelayedSports) || StopStreamingDelayedSports.Contains(sport);
        }
    }
}

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
        public Settings()
        {
            User = ConfigurationManager.AppSettings["user"];
            Password = ConfigurationManager.AppSettings["password"];
            Url = ConfigurationManager.AppSettings["url"];

            var value = ConfigurationManager.AppSettings["newFixtureCheckerFrequency"];
            FixtureCheckerFrequency = value == "" ? 60000 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["validateStreams"];
            ValidateStreams = value == "true";

            value = ConfigurationManager.AppSettings["fixtureCreationConcurrency"];
            FixtureCreationConcurrency = value == "" ? 20 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["startingRetryDelay"];
            StartingRetryDelay = value == "" ? 500 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["maxRetryDelay"];
            MaxRetryDelay = value == "" ? 65000 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["maxRetryAttempts"];
            MaxRetryAttempts = value == "" ? 3 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["echoInterval"];
            EchoInterval = value == "" ? 10000 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["echoDelay"];
            EchoDelay = value == "" ? 3000 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["suspendAllOnShutdown"];
            SuspendAllMarketsOnShutdown = value != "" && Convert.ToBoolean(value);

            value = ConfigurationManager.AppSettings["heartBeatIntervalSeconds"];
            HeartBeatIntervalSeconds = value == "" ? 60 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["eventStateFilePath"];
            EventStateFilePath = string.IsNullOrEmpty(value) ? @"C:\eventState.json" : Convert.ToString(value);

            value = ConfigurationManager.AppSettings["marketFiltersDirectory"];
            MarketFiltersDirectory = string.IsNullOrEmpty(value) ? @"MarketsState" : Convert.ToString(value);

            value = ConfigurationManager.AppSettings["numberOfListenersCreatedAtTime"];
            NumberOfListenersCreatedAtTime = value == "" ? 0 : Convert.ToInt32(value);

            value = ConfigurationManager.AppSettings["cacheExpiryInMins"];
            CacheExpiryInMins = value == "" ? 15 : Convert.ToInt32(value);
        }

        public string MarketFiltersDirectory { get; private set; }
        public int CacheExpiryInMins { get; private set; }

        public string User { get; private set; }

        public string Password { get; private set; }

        public string Url { get; private set; }

        public int FixtureCheckerFrequency { get; private set; }

        public bool ValidateStreams { get; private set; }

        public int StartingRetryDelay { get; private set; }

        public int MaxRetryDelay { get; private set; }

        public int MaxRetryAttempts { get; private set; }

        public int EchoInterval { get; private set; }

        public int EchoDelay { get; private set; }

        public bool SuspendAllMarketsOnShutdown { get; private set; }

        public int HeartBeatIntervalSeconds { get; private set; }

        public string EventStateFilePath { get; private set; }

        public int NumberOfListenersCreatedAtTime { get; set; }

        public int FixtureCreationConcurrency { get; set; }
    }
}

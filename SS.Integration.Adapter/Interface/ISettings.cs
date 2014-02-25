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


namespace SS.Integration.Adapter.Interface
{
    public interface ISettings
    {
        string User { get; }

        string Password { get; }

        string Url { get; }

        int FixtureCheckerFrequency { get; }

        int FixtureCreationConcurrency { get; }

        bool ValidateStreams { get; }

        int StartingRetryDelay { get; }

        int MaxRetryDelay { get; }

        int MaxRetryAttempts { get; }

        int EchoInterval { get; }

        int EchoDelay { get; }

        bool SuspendAllMarketsOnShutdown { get; }

        int HeartBeatIntervalSeconds { get; }

        string EventStateFilePath { get; }

        string MarketFiltersDirectory { get; }

        int CacheExpiryInMins { get;  }
     
    }
}

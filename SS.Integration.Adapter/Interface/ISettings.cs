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

namespace SS.Integration.Adapter.Interface
{
    public interface ISettings
    {
        /// <summary>
        /// Username of the Connect platform account
        /// </summary>
        string User { get; }

        /// <summary>
        /// Password of the Connect platform account
        /// </summary>
        string Password { get; }

        /// <summary>
        /// URL to the Connect platform
        /// </summary>
        string Url { get; }

        /// <summary>
        /// Returns the frequency in which
        /// the adapter checks for new fixtures
        /// </summary>
        int FixtureCheckerFrequency { get; }

        /// <summary>
        /// Returns the concurrency degree
        /// used when the adapter handles
        /// fixture creations
        /// </summary>
        int FixtureCreationConcurrency { get; }

        /// <summary>
        /// Delay used when trying to connect
        /// to the Connect plaftorm
        /// </summary>
        int StartingRetryDelay { get; }

        /// <summary>
        /// Delay limit - every time a connect attempt fails
        /// the StartingRetryDelay is multipled by two.
        /// This allows to put a upper limit to the delay value.
        /// </summary>
        int MaxRetryDelay { get; }

        /// <summary>
        /// Number of reconnect attempts
        /// </summary>
        int MaxRetryAttempts { get; }

        /// <summary>
        /// Echo interval delay
        /// </summary>
        int EchoInterval { get; }

        /// <summary>
        /// Echo delay
        /// </summary>
        int EchoDelay { get; }

        /// <summary>
        /// Determines wheter the adapter generates
        /// or not a suspension request for all the 
        /// markets when is shutting down
        /// </summary>
        bool SuspendAllMarketsOnShutdown { get; }

        /// <summary>
        /// Returns the path for
        /// the events' state storage file
        /// </summary>
        string EventStateFilePath { get; }

        /// <summary>
        /// Returns the path for the
        /// fixture's state storage
        /// </summary>
        string StateProviderPath { get; }

        /// <summary>
        /// Returns the directory
        /// where the adapter stores the 
        /// fixtures' statuses        
        /// </summary>
        string MarketFiltersDirectory { get; }

        /// <summary>
        /// Returns the expiration time
        /// (in minutes) for the adapter's cache
        /// </summary>
        int CacheExpiryInMins { get;  }

        /// <summary>
        /// True to enable DeltaRule
        /// </summary>
        bool DeltaRuleEnabled { get; }

        /// <summary>
        /// True to collect statistics
        /// </summary>
        bool StatsEnabled { get; }

        /// <summary>
        /// If != 0, the adapter will wait N
        /// minutes after a fixture receives
        /// a MatchOver message before
        /// stopping streaming.
        /// </summary>
        double StopStreamingDelayMinutes { get; }

        /// <summary>
        /// Comma separated list of sports for which
        /// the adapter should introduce a delay
        /// before stopping streaming
        /// after a fixture received a MatchOver message.
        /// 
        /// Null or empty string signigy "ALL" sports 
        /// </summary>
        string StopStreamingDelayedSports { get; }

        /// <summary>
        /// Indicates if the adapter should introduce
        /// a delay before stop streaming for fixtures
        /// that belong to a given sport
        /// </summary>
        /// <param name="sport"></param>
        /// <returns></returns>
        bool ShouldDelayStopStreaming(string sport);
    }
}

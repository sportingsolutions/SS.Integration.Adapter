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
        /// Returns the path for
        /// the events' state storage file
        /// </summary>
        string FixturesStateFilePath { get; }

        /// <summary>
        /// Returns the interval in miliseconds for how often to persist the fixtures state in the storage file
        /// </summary>
        int FixturesStateAutoStoreInterval { get; }

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
        int CacheExpiryInMins { get; }

        /// <summary>
        /// True to enable DeltaRule
        /// </summary>
        bool DeltaRuleEnabled { get; }

        /// <summary>
        /// True to collect statistics
        /// </summary>
        bool StatsEnabled { get; }

        /// <summary>
        /// True to instantiate the adapter's supervisor
        /// </summary>
        bool UseSupervisor { get; }

        int ProcessingLockTimeOutInSecs { get; }

        string SupervisorStatePath { get; }

        /// <summary>
        /// Allows skipping suspension on Prematch fixtures when disconnection occurs
        /// Prematch is less sensitive to price changes and some cusotmers prefer to keep the prices open 
        /// while Adapter is recovering
        /// </summary>
        bool DisablePrematchSuspensionOnDisconnection { get; }

        /// <summary>
        /// This is safety buffer for prematch fixtures when suspension on disconnection is disabled 
        /// The suspension will be applied if the fixture has only set amount of time left before start time 
        /// </summary>
        int PreMatchSuspensionBeforeStartTimeInMins { get; }

        /// <summary>
        /// Skips processing any rules (Delta rule/Void rule/...) when resending snapshot after the error occured in plugin
        /// </summary>
        bool SkipRulesOnError { get; }

        /// <summary>
        /// Number of sequences that Adapter can fall behind before the Stop streaming is called
        /// Setting this too very low value like 1 or 2 can cause StopStreaming being called during Plugin processing
        /// </summary>
        int StreamSafetyThreshold { get; }

        /// <summary>
        /// This is litit for sttempt to start streaming
        /// </summary>
        int StartStreamingTimeoutInSeconds { get; }

        /// <summary>
        /// This is the number of attempts the stream listener tries to start streaming before it is stopped on failure
        /// </summary>
        int StartStreamingAttempts { get; }

        /// <summary>
        /// This flag tells wether the fixture is allowed to start streaming when IsFixtureSetup is true
        /// </summary>
        bool AllowFixtureStreamingInSetupMode { get; }

        /// <summary>
        /// 
        /// </summary>
        bool IsSdkServiceCacheEnabled { get; }

        /// <summary>
        /// 
        /// </summary>
        int maxFixtureUpdateDelayInSeconds { get; }

        /// <summary>
        /// StreamHealthCheck disconnect streaming if fixture continoues staying in 
        /// Errored state more than MaxInErroredState
        /// </summary>
        int MaxInErroredState { get; }

        bool AutoReconnect { get; }

        /// <summary>
        /// This flag defines interval to recover fixture
        /// </summary>
        int delayedFixtureRecoveryAttemptSchedule { get; }
    }
}

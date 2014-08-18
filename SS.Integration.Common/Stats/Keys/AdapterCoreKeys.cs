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


namespace SS.Integration.Common.Stats.Keys
{
    public class AdapterCoreKeys
    {
        public static readonly string ADAPTER_STARTED = "started";
        public static readonly string ADAPTER_TOTAL_MEMORY = "memory_consumption";
        public static readonly string ADAPTER_HEALTH_CHECK = "health_check";
        public static readonly string ADAPTER_RUNNING_THREADS = "threads";
        public static readonly string ADAPTER_FIXTURE_TOTAL = "fixtures_total";

        public static readonly string SPORT_FIXTURE_TOTAL = "fixtures_total";
        public static readonly string SPORT_FIXTURE_IN_PLAY_TOTAL = "in_play_fixtures_total";
        public static readonly string SPORT_FIXTURE_STREAMING_TOTAL = "streaming_fixtures_total";

        public static readonly string FIXTURE_MARKETS_IN_SNAPSHOT = "markets_in_snapshot";
        public static readonly string FIXTURE_TOTAL_MARKETS = "markets_total";        
        public static readonly string FIXTURE_FILTERED_MARKETS = "markets_filtered_total";

        public static readonly string FIXTURE_SNAPSHOT_COUNTER = "snapshots_total";
        public static readonly string FIXTURE_ERRORS_COUNTER = "errors_total";

        public static readonly string FIXTURE_SNAPSHOT_PROCESSING_TIME = "snapshot_processing_time";
        public static readonly string FIXTURE_UPDATE_PROCESSING_TIME = "update_processing_time";

        public static readonly string FIXTURE_IS_IN_PLAY = "is_in_play";
        public static readonly string FIXTURE_IS_STREAMING = "is_streaming";
        public static readonly string FIXTURE_IS_MATCH_OVER = "is_match_over";

        public static readonly string MARKET_IS_ACTIVE = "is_active";
        public static readonly string MARKET_IS_RESULTED = "is_resulted";
        public static readonly string MARKET_IS_SUSPENDED = "is_suspended";
        public static readonly string MARKET_TOTAL_SELECTIONS = "selections_total";

        public static readonly string SELECTION_IS_ACTIVE = "is_active";
        public static readonly string SELECTION_IS_SETTLED = "is_settled";
        public static readonly string SELECTION_PRICE = "price";
    }
}

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
    public class PerformanceKeys
    {
        public static readonly string SNAPSHOT_PROCESSING_PROCESSING_TIME = "PERF_SNAPSHOT_TIME";
        public static readonly string DELTA_SNASPHOT_PROCESSING_TIME = "PERF_DELTA_SNAPSHOT_TIME";
        public static readonly string FIXTURE_CREATION_PROCESSING_TIME = "PERF_FIXTURE_INSERT_TIME";
        public static readonly string FIXTURE_UPDATE_PROCESSING_TIME = "PERF_FIXTURE_UPDATE_TIME";
        public static readonly string NUMBER_OF_REQUEST_ERRORS = "PERF_SUBMIT_ERROR";
        public static readonly string NUMBEER_OF_REQUESTS = "PERF_N_SUBMIT";
        public static readonly string TOTAL_CHUNK_REQUESTS_PROCESSING_TIME = "PERF_TOT_CHUNK_REQ_TIME";
        public static readonly string TOTAL_REQUEST_PROCESSING_TIME = "PERF_TOT_REQ_TIME";
    }
}

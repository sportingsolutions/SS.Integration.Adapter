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
    public class PluginCoreKeys
    {
        public static readonly string PLUGIN_SNAPSHOT_PROCESSING_TIME = "performance.snapshot_processing_time";
        public static readonly string PLUGIN_DELTA_SNASPHOT_PROCESSING_TIME = "performance.update_processing_time";
        public static readonly string PLUGIN_ERRORED_REQUESTS = "error.requests_errored";
        public static readonly string PLUGIN_REQUEST_SERVICE_UNAVAILABLE = "error.503";
        public static readonly string PLUGIN_PROCESSED_REQUESTS = "performance.requests_processed";
        public static readonly string PLUGING_REQUEST_PROCESSING_TIME = "performance.request_processing_time";

        public static readonly string PLUGING_SCHEDULER_UPDATE_QUEUE_SIZE = "scheduler.queue.update";
        public static readonly string PLUGING_SCHEDULER_INSERT_QUEUE_SIZE = "scheduler.queue.insert";
        public static readonly string PLUGING_SCHEDULER_SETTLEMENT_QUEUE_SIZE = "scheduler.queue.settlement";
        
    }
}

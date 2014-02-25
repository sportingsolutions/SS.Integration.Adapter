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
    public class SchedulingKeys
    {
        public static readonly string UPDATE_QUEUE = "SCHED_UPDATE_COUNT";
        public static readonly string INSERT_QUEUE = "SCHED_INSERT_COUNT";
        public static readonly string SETTLEMENT_QUEUE = "SCHED_SETTLEMENT_COUNT";
        public static readonly string UPDATE_QUEUE_MAX = "SCHED_UPDATE_MAX";
        public static readonly string INSERT_QUEUE_MAX = "SCHED_INSERT_MAX";
        public static readonly string SETTLEMENT_QUEUE_MAX = "SCHED_SETTLEMENT_MAX";
        public static readonly string CONCURRENCY_DEGREE = "SCHED_N_THREADS";
    }
}

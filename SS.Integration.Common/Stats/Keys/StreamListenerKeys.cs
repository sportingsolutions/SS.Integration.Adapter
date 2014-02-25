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
    public class StreamListenerKeys
    {
        public static readonly string STATUS = "STREAM_STATUS";
        public static readonly string LAST_SEQUENCE = "STREAM_LAST_SEQUENCE";
        public static readonly string LAST_INVALID_SEQUENCE = "STREAM_LAST_INVALID_SEQUENCE";
        public static readonly string FIXTURE = "STREAM_FIXTURE";
        public static readonly string RESTARTED = "STREAM_RESTARTED";
        public static readonly string SNAPSHOT_RETRIEVED = "STREAM_SNAPSHOTS";
        public static readonly string UPDATE_PROCESSED = "STREAM_UPDATE";
        public static readonly string INVALID_SEQUENCE = "STREAM_INVALID_SEQUENCE";
        public static readonly string INVALID_EPOCH = "STREAM_INVALID_EPOCH";
    }
}

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

namespace SS.Integration.Common.Stats.Interface
{
    public interface IStatsConsumer
    {

        string Name { get; set; }

        bool IsEnabled { get; set; }

        void AddSettingProperty(string key, string value);

        string GetSettingProperty(string key);

        void AddValue(string key, string value);

        void SetValue(string key, string value);

        void IncrementValue(string key);

        void Configure();
    }
}

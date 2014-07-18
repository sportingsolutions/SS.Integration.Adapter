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
using System.Linq;
using SS.Integration.Common.Stats.Interface;

namespace SS.Integration.Common.Stats.Configuration
{
    public class StatsSettings
    {
        private readonly Dictionary<string, IStatsConsumer> _consumers;

        public StatsSettings()
        {
            _consumers = new Dictionary<string, IStatsConsumer>();
        }

        public bool IsEnabled { get; set; }

        public IEnumerable<string> Consumers
        {
            get
            {
                return _consumers.Select(consumer => consumer.Value.Name);
            }
        }

        public void AddConsumer(IStatsConsumer consumer)
        {
            if (consumer == null)
                return;

            _consumers[consumer.Name] = consumer;
        }

        public IStatsConsumer GetConsumer(string name)
        {
            return string.IsNullOrEmpty(name) ? null : _consumers[name];
        }
    }
}

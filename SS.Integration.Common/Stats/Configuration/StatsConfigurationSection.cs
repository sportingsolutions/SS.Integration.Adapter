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

using System;
using System.Configuration;
using System.Linq;
using System.Xml.Linq;
using SS.Integration.Common.Stats.Interface;

namespace SS.Integration.Common.Stats.Configuration
{
    public class StatsConfigurationSection : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, System.Xml.XmlNode section)
        {
            var configuration = new StatsSettings {IsEnabled = false};

            var doc = XDocument.Parse(section.OuterXml);

            var metric = doc.Element("StatsSettings");

            if (metric == null)
                return configuration;

            var attribute = metric.Attribute("enabled");
            if (attribute != null)
                configuration.IsEnabled = string.Equals(attribute.Value, "true", StringComparison.OrdinalIgnoreCase);


            foreach (var consumer in metric.Elements("consumer"))
            {
                attribute = consumer.Attribute("name");
                if (attribute == null)
                    continue;

                var name = attribute.Value;

                if (string.IsNullOrEmpty(name))
                    continue;

                attribute = consumer.Attribute("type");
                if (attribute == null)
                    continue;

                var type = attribute.Value;

                if(string.IsNullOrEmpty(type))
                    continue;

                IStatsConsumer consumer_obj = null;
                try 
                {

                    Type consumer_type = Type.GetType(attribute.Value);
                    if(consumer_type != null)
                        consumer_obj = Activator.CreateInstance(consumer_type) as IStatsConsumer;
                }
                catch {}

                if (consumer_obj == null)
                    continue;

                consumer_obj.Name = name;
                consumer_obj.IsEnabled = true;

                attribute = consumer.Attribute("enabled");
                if (attribute != null)
                {
                    consumer_obj.IsEnabled = string.Equals(attribute.Value, "true");
                }

                foreach (var setting in consumer.Elements("add").ToDictionary(x => x.Attribute("key").Value, v => v.Attribute("value").Value))
                {
                    consumer_obj.AddSettingProperty(setting.Key, setting.Value);
                }


                try
                {
                    consumer_obj.Configure();
                    configuration.AddConsumer(consumer_obj);
                }
                catch { }
            }

            return configuration;
        }
    }
}

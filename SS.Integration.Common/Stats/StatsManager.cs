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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Xml;
using SS.Integration.Common.Stats.Configuration;
using SS.Integration.Common.Stats.Interface;

namespace SS.Integration.Common.Stats
{
    public class StatsManager
    {
        private const string DEFAULT_CONF_FILE_NAME = "stats.config";
        private static StatsManager _instance;

        private readonly StatsSettings _settings;
        private readonly ConcurrentDictionary<string, StatsManager> _managers;
        
        private IStatsHandle _handle;


        private StatsManager(string code, StatsSettings settings)
        {
            Code = code;
            _managers = new ConcurrentDictionary<string, StatsManager>();
            _settings = settings;
        }

        public static StatsManager Instance
        {
            get { return _instance ?? (_instance = new StatsManager("Root", null)); }
        }

        public static void Configure(string filename = DEFAULT_CONF_FILE_NAME)
        {
            var settings = GetSettings(filename);
            if (settings != null && settings.IsEnabled)
            {
                _instance = new StatsManager("Root", settings);
            }
        }

        public string Code { get; private set; }

        public IEnumerable<IStatsConsumer> Consumers
        {
            get
            {
                if (_settings != null)
                {
                    foreach (var consumer_name in _settings.Consumers)
                    {
                        IStatsConsumer consumer = _settings.GetConsumer(consumer_name);
                        if (consumer != null && consumer.IsEnabled)
                            yield return consumer;
                    }
                }
            }
        }

        public IStatsConsumer GetConsumer(string name)
        {
            if (_settings != null)
                _settings.GetConsumer(name);

            return null;
        }

        public StatsManager this[string code] 
        {
            get{

                if(string.IsNullOrEmpty(code))
                    return this;

                if (!_managers.ContainsKey(code))
                    _managers.TryAdd(code, new StatsManager(code, _settings));
    
                return _managers[code];
            }
        }

        public IStatsHandle GetHandle()
        {
            return _handle ?? (_handle = new StatsHandle(this));
        }

        private static StatsSettings GetSettings(string filename)
        {
            try
            {
                var filepath = filename;
                if (!Path.IsPathRooted(filename))
                {
                    Assembly ass = Assembly.GetCallingAssembly();

                    filepath = Path.Combine(Path.GetDirectoryName(ass.Location), filename);
                }

                if (!File.Exists(filepath))
                    return null;

                var fileMap = new ExeConfigurationFileMap { ExeConfigFilename = filepath };
                var configuration = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);

                ConfigurationSection section = section = configuration.GetSection("StatsSettings");
                if (section == null)
                    return null;

                string xml = section.SectionInformation.GetRawXml();
                Type type = Type.GetType(section.SectionInformation.Type);

                if (typeof(IConfigurationSectionHandler).IsAssignableFrom(type))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(XmlReader.Create(new StringReader(xml)));

                    IConfigurationSectionHandler configSectionHandlerHandle = Activator.CreateInstance(type) as IConfigurationSectionHandler;
                    if (configSectionHandlerHandle != null)
                    {
                        return configSectionHandlerHandle.Create(null, null, doc) as StatsSettings;
                    }
                }
            }
            catch { }
            
            return null;
        }
    }
}

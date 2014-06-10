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
using System.IO;
using System.Reflection;
using System.Xml;

namespace SS.Integration.Adapter.Model
{
    public class ModuleConfigurationProvider
    {
        public static Configuration GetModuleConfiguration(string filename)
        {
            Assembly ass = Assembly.GetCallingAssembly();
            return ReadInternal(ass, filename);
        }

        public static object ReadSection(Configuration conf, string sectionname)
        {
            ConfigurationSection section = null;
            if (conf == null || (section = conf.GetSection(sectionname)) == null)
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
                    return configSectionHandlerHandle.Create(null, null, doc);
                }
            }
            return xml;
        }

        private static Configuration ReadInternal(Assembly assembly, string filename)
        {
            var filepath = Path.Combine(Path.GetDirectoryName(assembly.Location), filename);
            
            var fileMap = new ExeConfigurationFileMap {ExeConfigFilename = File.Exists(filepath) ? filepath : filename};
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
            
            return configuration;
        }
    }
}

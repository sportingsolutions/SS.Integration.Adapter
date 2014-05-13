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
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Xml.Linq;
using SS.Integration.Adapter.Plugin.Model;
using SS.Integration.Common.ConfigSerializer;

namespace SS.Integration.Adapter.Configuration
{
    public class MappingUpdaterSection : IConfigurationSectionHandler
    {
        public object Create(object parent, object configContext, System.Xml.XmlNode section)
        {
            var doc = XDocument.Parse(section.OuterXml);
            MappingUpdaterConfiguration result = new MappingUpdaterConfiguration();
            SetProperties(result,
                              doc.Element("mappingUpdater")
                                .Element("generalConfig")
                                .Elements()
                                .ToDictionary(x => x.Attribute("key").Value, v => v.Attribute("value").Value, StringComparer.InvariantCultureIgnoreCase));

            if (!String.IsNullOrEmpty(result.SerializerSettingsSection) && !String.IsNullOrEmpty(result.SerializerSettingsClass))
            {
                if (doc.Element("mappingUpdater").Element(result.SerializerSettingsSection) != null)
                {
                    Type serializerSettingsType =Type.GetType(result.SerializerSettingsClass);
                    if (serializerSettingsType != null)
                    {
                        IConfigSerializerSettings configSerializerSettings = (IConfigSerializerSettings) Activator.CreateInstance(serializerSettingsType);
                        SetProperties(configSerializerSettings,
                                      doc.Element("mappingUpdater")
                                         .Element(result.SerializerSettingsSection)
                                         .Elements()
                                         .ToDictionary(x => x.Attribute("key").Value, v => v.Attribute("value").Value,
                                                       StringComparer.InvariantCultureIgnoreCase));
                        result.SerializerSettings = configSerializerSettings;
                    }
                }
            }

            return result; 
        }

        private bool IsNullable(Type type)
        {
            return Nullable.GetUnderlyingType(type) != null;
        }

        private void SetProperties(object configuration, Dictionary<string, string> settings)
        {
            foreach (var property in configuration.GetType().GetProperties())
            {
                if (settings.ContainsKey(property.Name))
                {
                    var conversionType = property.PropertyType;
                    if (IsNullable(property.PropertyType) && !string.IsNullOrEmpty(property.Name))
                    {
                        conversionType = property.PropertyType.GetGenericArguments()[0];
                    }

                    property.SetValue(configuration,
                                          Convert.ChangeType(settings[property.Name], conversionType), null);
                }
            }
        }
    }
}

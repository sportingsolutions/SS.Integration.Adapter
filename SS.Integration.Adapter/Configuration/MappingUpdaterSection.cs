using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            if (doc.Element("mappingUpdater").Element("google") != null)
            {
                GoogleDocSettings googleSettings = new GoogleDocSettings();
                SetProperties(googleSettings,
                              doc.Element("mappingUpdater")
                                 .Element("google")
                                 .Elements()
                                 .ToDictionary(x => x.Attribute("key").Value, v => v.Attribute("value").Value,
                                               StringComparer.InvariantCultureIgnoreCase));
                result.SerializerSettings = googleSettings;
            }
            SetProperties(result,
                              doc.Element("mappingUpdater")
                                .Element("generalConfig")
                                .Elements()
                                .ToDictionary(x => x.Attribute("key").Value, v => v.Attribute("value").Value, StringComparer.InvariantCultureIgnoreCase));
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

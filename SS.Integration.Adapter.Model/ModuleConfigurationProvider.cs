using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting;
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

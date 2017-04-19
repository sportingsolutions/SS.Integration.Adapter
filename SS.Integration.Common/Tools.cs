using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace SS.Integration.Common
{
    public static class Tools
    {
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
        /// <summary>
        /// Combine relative path to full depend on executing assembly directory
        /// </summary>
        /// <param name="relativePath">relative Path</param>
        /// <returns>AbsolutePath</returns>
        public static string GetAbsolutePath(string relativePath)
        {
            return Path.Combine(AssemblyDirectory, relativePath);
        }

        #region Serializing
       
        public static bool SerializeXml(string filePath, object objectToSer)
        {
            var typeToSer = objectToSer.GetType();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            var ser = new XmlSerializer(typeToSer, new[] { typeToSer });

            using (TextWriter writer = new StreamWriter(filePath))
            {
                  ser.Serialize(writer, objectToSer);
            }
            return true;
        }
        public static T DeserializeXml<T>(string filePath) where T : class
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                var ser = new XmlSerializer(typeof(T), new[] { typeof(T) });

                using (TextReader reader = new StreamReader(filePath))
                {
                    return (T)ser.Deserialize(reader);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
 
        public static T DeserializeBinary<T>(this string filePath)
        {
            using (var ms = File.OpenRead(filePath))
            {
                var ser = new BinaryFormatter();
                return (T)ser.Deserialize(ms);
            }
        }

        public static bool SerializeBinary<T>(this T t, string filePath) where T : class
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            using (var ms = File.Create(filePath))
            {
                var ser = new BinaryFormatter();
                ser.Serialize(ms, t);
                return true;
            }
            return false;
        }
        #endregion
    }
}

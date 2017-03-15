using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SS.Integration.Common.Converters
{
    public static class ReflectionXmlSerializer
    {
        public static void Serialize(string filePath, object objectToSer)
        {
            var doc = objectToSer.ToXmlDocument();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            doc.Save(filePath);
        }
        public static XmlDocument ToXmlDocument(this object obj)
        {
            XmlDocument xmlDoc = new XmlDocument();
            XmlDeclaration xmlDec = xmlDoc.CreateXmlDeclaration("1.0", "utf-8", String.Empty);
            xmlDoc.PrependChild(xmlDec);
            var node = Process(xmlDoc, obj, obj.GetType(), null);
            xmlDoc.AppendChild(node);
            return xmlDoc;
        }


        private static void FillXmlElement(object obj, XmlDocument xmlDoc, XmlElement elemRoot, bool isCollectionElement = false)
        {
            try
            {

                var type = obj.GetType();
                if (isCollectionElement)
                {
                    var elem = xmlDoc.CreateElement(type.Name.correctName());
                    elemRoot.AppendChild(elem);
                    if (type.IsValueType || type == typeof(string))
                    {
                        elem.InnerText = obj.ToString();
                        return;
                    }
                    else
                    {
                        elemRoot = elem;
                    }
                }

                foreach (PropertyInfo pInfo in type.GetProps())
                {
                    try
                    {
                        object o = pInfo.GetValue(obj, null);
                        if (o == null)
                            continue;
                        var oType = o == null ? pInfo.PropertyType : o.GetType();
                        XmlElement elem = Process(xmlDoc, o, oType, pInfo.Name);
                        elemRoot.AppendChild(elem);
                    }
                    catch   { }
                }
            }
            catch  { }
        }

        private static XmlElement Process(XmlDocument xmlDoc, object o, Type oType, string name)
        {
            if (name == null)
                name = oType.Name;
            name = correctName(name);
            var elem = xmlDoc.CreateElement(name);

            if (oType.IsValueType || oType == typeof(string))
                elem.InnerText = o.ToString();
            else
            {
                if (typeof(IEnumerable).IsAssignableFrom(oType))
                    foreach (var item in (IEnumerable)o)
                    {
                        FillXmlElement(item, xmlDoc, elem, true);
                    }
                else
                    FillXmlElement(o, xmlDoc, elem);
            }

            return elem;
        }

        static string correctName(this string name)
        {
            var s = string.Concat(name.Where(x => char.IsLetterOrDigit(x)));
            return string.IsNullOrEmpty(s) ? "Unknown" : s;
        }
        private static PropertyInfo[] GetProps(this Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.CanRead)
                    .ToArray();
        }
    }
}

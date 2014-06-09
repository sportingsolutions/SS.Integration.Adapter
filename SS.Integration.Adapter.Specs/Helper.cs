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
using System.ComponentModel;
using System.Linq;
using SS.Integration.Adapter.Model;
using TechTalk.SpecFlow;

namespace SS.Integration.Adapter.Specs
{
    public class Helper 
    {
        public static Market GetMarketFromTable(Table table)
        {
            var market = new Market {Id = "TestId"};
            market.AddOrUpdateTagValue("name", "TestMarket");
            market.Selections.Clear();
            market.Selections.AddRange(table.Rows.Select(r => GetObjectFromTableRow<Selection>(r)));

            return market;
        }

        public static T GetObjectFromTableRow<T>(TableRow row, string separator = null) where T : class, new()
        {
            var newObject = new T();
            foreach (var propertyName in row.Keys)
            {
                var property = typeof(T).GetProperty(propertyName);
                if (property == null)
                    continue;

                TypeConverter conv = TypeDescriptor.GetConverter(property.PropertyType);

                // is collection property
                if (!string.IsNullOrEmpty(separator) && row[propertyName].Contains(separator))
                {
                    var dictionaryType = property.PropertyType.GetGenericArguments()[1];
                    var isStringTypeDictionary = dictionaryType == typeof(string);
                    if (isStringTypeDictionary)
                        property.SetValue(newObject, GetDictionary<string>(row[propertyName], separator));
                    else
                        property.SetValue(newObject, GetDictionary<object>(row[propertyName], separator));

                }
                else
                {
                    var propertyValue = conv.ConvertFrom(row[propertyName]);
                    property.SetValue(newObject, propertyValue);
                }
            }

            return newObject;
        }

        public static IDictionary<string, T> GetDictionary<T>(string value, string separator) where T : class
        {
            var outputDictionary = new Dictionary<string, T>();
            var values = value.Split(separator[0]);
            foreach (var keyValueItem in values)
            {
                var keyValuePair = keyValueItem.Split('=').Select(v => v.Trim());
                outputDictionary.Add(keyValuePair.First(), keyValuePair.Last() as T);
            }

            return outputDictionary;
        }

    }
}

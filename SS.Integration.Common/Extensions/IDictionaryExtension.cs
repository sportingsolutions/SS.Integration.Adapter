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
using System.Linq;
using System.Reflection;

namespace SS.Integration.Common.Extensions
{
    public static class IDictionaryExtension
    {
        public static TValue GetKeyOrDefault<TKey,TValue>(this IDictionary<TKey,TValue> dictionary,TKey key)
        {
            if (dictionary == null || string.IsNullOrEmpty(key.ToString()) || !dictionary.ContainsKey(key))
                return default(TValue);

            return dictionary[key];
        }

        public static TValue ValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key)
        {
            TValue res;
            if (dic.TryGetValue(key, out res))
                return res;
            return default(TValue);
        }
        public static bool ValueEquals<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key1, TKey key2)
        {

            var v1 = dic.ValueOrDefault(key1);
            var v2 = dic.ValueOrDefault(key2);
            return v1 != null && v2 != null && v1.Equals(v2);
        }
        public static string StringOrDefault(this IDictionary<string, object> dic, string key)
        {
            return dic.ValueOrDefault(key)?.ToString();
        }


        public static void SetFromDictionary(this object target, IDictionary<string, object> dic, string propertyName, string key)
        {
            if (target == null)
                return;

            if (dic == null)
                return;

            if (dic.ContainsKey(key))
            {
                PropertyInfo property = target.GetType().GetProperty(propertyName);
                if (property != null)
                    property.SetValue(target, dic[key]);
            }
        }

        public static void SetFromDictionary(this object target, Dictionary<string, object> dic, string propertyName)
        {
            target.SetFromDictionary(dic, propertyName, propertyName);
        }

        public static void SetFromDictionary(this Dictionary<string, object> dic, string key, ref string value)
        {
            if (dic.ContainsKey(key))
            {
                value = dic[key]?.ToString();
            }
        }

        public static bool IsTagValueMatch(this Dictionary<string, object> dic, string key, string value, bool caseSensitive = false)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (string.IsNullOrEmpty(value))
                return false;

            object tagValue = null;
            if (dic.TryGetValue(key, out tagValue))
            {
                return tagValue.ToString().Equals(value,
                    caseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase);
            }

            return false;
        }

        public static string FindKey(this Dictionary<string, object> dic, string key, bool keyCaseSensitive = true)
        {
            return dic.Keys.FirstOrDefault(_ => _.Equals(key, keyCaseSensitive
                ? StringComparison.InvariantCulture
                : StringComparison.InvariantCultureIgnoreCase));
        }

        public static string FindKey(this Dictionary<string, string> dic, string key, bool keyCaseSensitive = true)
        {
            return dic.Keys.FirstOrDefault(_ => _.Equals(key, keyCaseSensitive
                ? StringComparison.InvariantCulture
                : StringComparison.InvariantCultureIgnoreCase));
        }

        public static string GetValue(this Dictionary<string, string> dic, string key, bool keyCaseSensitive = true)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            var existingKey = dic.FindKey(key, keyCaseSensitive);
            return !string.IsNullOrEmpty(existingKey)
                ? dic[existingKey]
                : null;
        }

        public static void AddOrUpdateValue(this Dictionary<string, string> dic, string key, string value, bool keyCaseSensitive = true)
        {
            if (string.IsNullOrEmpty(key))
                return ;

            var existingKey = dic.FindKey(key, keyCaseSensitive);

            if (existingKey != null)
            {
                dic[existingKey] = value;
            }
            else
            {
                dic[keyCaseSensitive ? key : key.ToLower()]= value;

            }
            
        }
        
        public static bool IsValueMatch(this Dictionary<string, string> dic, string key, string value, 
            bool valueCaseSensitive = false, bool keyCaseSensitive = true)
        {
            return !string.IsNullOrEmpty(key)
                && !string.IsNullOrEmpty(value)
                && value.Equals(GetValue(dic,key, keyCaseSensitive),
                    valueCaseSensitive
                        ? StringComparison.InvariantCulture
                        : StringComparison.InvariantCultureIgnoreCase);
        }


    }
}

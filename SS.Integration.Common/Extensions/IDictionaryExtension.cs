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
    }
}

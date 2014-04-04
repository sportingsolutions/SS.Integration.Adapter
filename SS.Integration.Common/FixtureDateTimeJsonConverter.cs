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
using Newtonsoft.Json.Converters;

namespace SS.Integration.Common
{
    public class FixtureDateTimeJsonConverter : IsoDateTimeConverter 
    {
        private static FixtureDateTimeJsonConverter _Instance;

        private FixtureDateTimeJsonConverter() { }

        public static FixtureDateTimeJsonConverter Instance
        {
            get { return _Instance ?? (_Instance = new FixtureDateTimeJsonConverter()); }
        }


        public override object ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            object ret = base.ReadJson(reader, objectType, existingValue, serializer);
            if (ret is DateTime)
            {
                DateTime val = (DateTime)ret;
                return new DateTime(val.Year, val.Month, val.Day, val.Hour, val.Minute, val.Second);
            }

            return ret;
        }
    }
}

﻿//Copyright 2014 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.


using Newtonsoft.Json;
using SS.Integration.Adapter.Model;
using SS.Integration.Common;

namespace SS.Integration.Adapter
{
    public static class FixtureHelper
    {
        #region Public methods

        public static Fixture GetFromJson(string content)
        {
            return JsonConvert.DeserializeObject<Fixture>(content, FixtureDateTimeJsonConverter.Instance);
        }

        public static string ToJson(Fixture fixture)
        {
            return JsonConvert.SerializeObject(fixture);
        }

        public static Fixture GetFixtureDelta(StreamMessage streamMessage)
        {
            return JsonConvert.DeserializeObject<Fixture>(
                streamMessage.Content.ToString(),
                FixtureDateTimeJsonConverter.Instance);
        }

        #endregion
    }
}

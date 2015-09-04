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
using SS.Integration.Adapter.Diagnostics.Model.Interface;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    [Serializable]
    public class SportOverview : ISportOverview, IEquatable<SportOverview>
    {
        public string Name { get; set; }
        public int Total { get; set; }
        public int InPlay { get; set; }
        public int InSetup { get; set; }
        public int InPreMatch { get; set; }
        public int InErrorState { get; set; }
        
        public bool Equals(SportOverview other)
        {
            var areObjectsEqual = other != null;

            foreach (var property in typeof(SportOverview).GetProperties())
            {
                areObjectsEqual = property.GetValue(this).Equals(property.GetValue(other));
                if(!areObjectsEqual)
                    break;
            }

            return areObjectsEqual;
        }
    }
}


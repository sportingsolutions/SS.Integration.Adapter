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

namespace SS.Integration.Adapter.Tests
{
    public class GenericComparerHelper<T,P> : IEqualityComparer<T>
    {
        private readonly Func<T, P> _propertyExpression;

        public GenericComparerHelper(Func<T,P> propertyExpression)
        {
            _propertyExpression = propertyExpression;
        }

        public bool Equals(T x, T y)
        {
            var firstObj = _propertyExpression(x);
            var secondObj = _propertyExpression(y);

            return firstObj != null && firstObj.Equals(secondObj);
        }

        public int GetHashCode(T obj)
        {
            throw new NotImplementedException();
        }
    }
}

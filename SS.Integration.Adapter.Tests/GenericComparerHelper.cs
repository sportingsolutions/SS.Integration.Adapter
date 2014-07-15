using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

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

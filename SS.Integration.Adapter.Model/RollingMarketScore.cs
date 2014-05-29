using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Model
{
    [Serializable]
    public class RollingMarketScore
    {
         public double? Home { get; private set; }
         public double? Away { get; private set; }
         public double? Total { get; private set; }
    }
}

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
        public double? Home { get; set; }
        public double? Away { get; set; }
        public double? Total { get; set; }
    }
}

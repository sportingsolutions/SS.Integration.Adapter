using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Model
{
    [Serializable]
    public class RollingMarket : Market
    {
        public RollingMarket()
        {
            Selections = new List<RollingSelection>();
        }

        // <summary>
        /// Returns the rolling selections's for this market
        /// as they are contained within the snapshot.
        /// </summary>
        public virtual List<RollingSelection> Selections { get; private set; }
        public double Line { get; set; }
        public RollingMarketScore Score { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            
        }

        // <summary>
        /// Returns the rolling selections's for this market
        /// as they are contained within the snapshot. 
        /// </summary>
        // DO NOT CHANGE it to List, as List is mutable and we need to keep 
        // both base and this object in sync
        public ReadOnlyCollection<RollingSelection> Selections { 
            get { return _selections.Cast<RollingSelection>().ToList().AsReadOnly(); }
            set { _selections = value.Cast<Selection>().ToList(); } 
        }

        public double? Line
        {
            get; set;
        }

        public RollingMarketScore Score { get; set; }
    }
}

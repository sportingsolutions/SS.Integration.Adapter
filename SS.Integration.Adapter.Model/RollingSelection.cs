using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Model
{
    [Serializable]
    public class RollingSelection : Selection
    {
        public double Line { get; set; }
    }
}

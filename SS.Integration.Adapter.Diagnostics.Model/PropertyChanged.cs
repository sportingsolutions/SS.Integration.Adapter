using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class PropertyChanged
    {
        
        public string ItemName { get; internal set; }

        public string CurrentValue { get; set;  }
        
        internal void SetTimeStamp()
        {
            TimeStamp = DateTime.UtcNow;
        }

        public DateTime TimeStamp { get; private set; }

        public string PreviousValue { get; internal set; }

        public override string ToString()
        {
            string.Format("{0} changed from {1} to {2}", ItemName, PreviousValue, CurrentValue);
        }
    }
}

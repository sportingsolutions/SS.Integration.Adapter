using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Adapter.Diagnostics.Model.Interface;

namespace SS.Integration.Adapter.Diagnostics.Model
{
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

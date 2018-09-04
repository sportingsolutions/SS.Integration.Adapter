using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Actors
{
    public class UpdateProcessing
    {
        public bool IsSnapshot { get; set; }
        public DateTime Time { get; set; }
        public int Sequence { get; set; }
        public string FixtureName { get; set; }
        public string PluginMethod { get; set; }

        public override string ToString()
        {
            return $"{PluginMethod} {FixtureName} Time={Time.ToString("T")}";
        }
    }
}

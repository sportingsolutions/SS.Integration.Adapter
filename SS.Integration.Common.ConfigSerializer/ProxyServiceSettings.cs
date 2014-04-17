using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Common.ConfigSerializer
{
    public class ProxyServiceSettings : IConfigSerializerSettings
    {
        public string ReadMappingServiceUrl { get; set; }
        public string CheckUpdateServiceUrl { get; set; }
        public string SportListServiceUrl { get; set; }
        public string Company { get; set; }
        public string Enviroment { get; set; }
    }
}

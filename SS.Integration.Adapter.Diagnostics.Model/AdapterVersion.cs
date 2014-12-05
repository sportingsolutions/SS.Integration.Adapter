using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public class AdapterVersionInfo : IAdapterVersion
    {
        public string UdapiSDKVersion { get; private set; }
        public string AdapterVersion { get; private set; }
        public string PluginName { get; private set; }
        public string PluginVersion { get; private set; }
    }
}

using SS.Integration.Adapter.Plugin.Model.Interface;
using SS.Integration.Adapter.Plugin.Model;

namespace SS.Integration.Adapter.Mappings
{
    public class DummyMappingUpdaterFactory : IMappingUpdaterFactory
    {
        public MappingUpdaterConfiguration Configuration { get; set; }

        public IMappingUpdater GetMappingUpdater()
        {
            return new DummyMappingUpdater();
        }
    }
}

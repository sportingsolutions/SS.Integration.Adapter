using SS.Integration.Adapter.Plugin.Model;
using SS.Integration.Adapter.Plugin.Model.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Mappings
{
    public class DefaultMappingsCollectionProvider : IMappingsCollectionProvider
    {
        private IMappingUpdater _mappingUpdater = null;
        public DefaultMappingsCollectionProvider(IMappingUpdater mappingUpdater)
        {
            _mappingUpdater = mappingUpdater;
        }

        public IMappingsCollection GetMappingsCollection()
        {
            MappingsCollection result = new MappingsCollection();
            _mappingUpdater.Observers.Add(result);
            _mappingUpdater.Initialize();
            return result;
        }
    }
}

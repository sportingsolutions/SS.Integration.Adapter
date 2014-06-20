using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Common.ConfigSerializer.MappingUpdater.Interfaces;

namespace SS.Integration.Common.ConfigSerializer.MappingUpdater
{
    public class DefaultCachedMappingsStorage<T> : ICachedMappingsStorage<T>
    {
        public void SaveMappingsInCache(List<T> mappings)
        {
            throw new NotImplementedException();
        }

        public List<T> GetMappingsInCache()
        {
            throw new NotImplementedException();
        }
    }
}

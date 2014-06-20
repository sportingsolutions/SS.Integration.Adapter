using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Common.ConfigSerializer.MappingUpdater.Interfaces
{
    public interface ICachedMappingsStorage<T>
    {
        void SaveMappingsInCache(List<T> mappings);
        List<T> GetMappingsInCache();
    }
}

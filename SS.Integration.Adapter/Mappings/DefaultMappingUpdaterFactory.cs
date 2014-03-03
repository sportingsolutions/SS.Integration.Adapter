using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Adapter.Plugin.Model;
using SS.Integration.Adapter.Plugin.Model.Interface;
using SS.Integration.Adapter.ProcessState;
using SS.Integration.Common.ConfigSerializer;

namespace SS.Integration.Adapter.Mappings
{
    public class DefaultMappingUpdaterFactory : IMappingUpdaterFactory 
    {
        public MappingUpdaterConfiguration Configuration { get; set; }

        public IMappingUpdater GetMappingUpdater()
        {
            var configSerializer = new GoogleDocConfigSerializer();
            configSerializer.Settings = (GoogleDocSettings)this.Configuration.SerializerSettings;
            var mappingUpdater = new DefaultMappingUpdater();
            mappingUpdater.Serializer = configSerializer;
            mappingUpdater.CheckForUpdatesInterval = this.Configuration.CheckForUpdatesInterval;
            mappingUpdater.CachedObjectProvider =
                new BinaryStoreProvider<List<Mapping>>("CachedMappings", "{0}.bin");
            mappingUpdater.FileNameOrReference = this.Configuration.FileNameOrReferenceToDeserialize;
            return mappingUpdater;
        }


    }
}

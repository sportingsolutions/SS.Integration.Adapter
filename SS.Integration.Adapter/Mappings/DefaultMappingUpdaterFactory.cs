//Copyright 2014 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.


using System.Collections.Generic;
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
            var configSerializer = new ProxyServiceConfigSerializer((ProxyServiceSettings)this.Configuration.SerializerSettings);
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

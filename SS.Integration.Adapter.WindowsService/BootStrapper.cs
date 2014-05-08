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

using System;
using Ninject.Modules;
using SS.Integration.Adapter.Configuration;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Mappings;
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.Plugin.Model;
using SS.Integration.Adapter.Plugin.Model.Interface;
using SS.Integration.Adapter.UdapiClient;
using System.Configuration;

namespace SS.Integration.Adapter.WindowsService
{
    public class BootStrapper : NinjectModule
    {
        public override void Load()
        {
            Bind<ISettings>().To<Settings>().InSingletonScope();
            Bind<IReconnectStrategy>().To<DefaultReconnectStrategy>().InSingletonScope();
            Bind<IServiceFacade>().To<UdapiServiceFacade>();

            var mappingUpdaterSetting = ConfigurationManager.GetSection("mappingUpdater") as MappingUpdaterConfiguration;
            Type mappingUpdaterFactoryType = Type.GetType(mappingUpdaterSetting.MappingUpdaterFactoryClass);
            if (mappingUpdaterFactoryType == null)
                throw new ApplicationException(
                    string.Format(
                        "Couldn't load MappingUpdaterFactory type of: {0}",
                        mappingUpdaterSetting.MappingUpdaterFactoryClass));

            IMappingUpdaterFactory mappingUpdaterFactInstance = (IMappingUpdaterFactory)Activator.CreateInstance(mappingUpdaterFactoryType);
            mappingUpdaterFactInstance.Configuration = mappingUpdaterSetting;
            IMappingUpdater mappingUpdater = mappingUpdaterFactInstance.GetMappingUpdater();

            Bind<IMappingUpdater>().ToConstant(mappingUpdater);

            IMappingsCollectionProvider mapCollProvider = new DefaultMappingsCollectionProvider(mappingUpdater);
            Bind<IMappingsCollectionProvider>().ToConstant(mapCollProvider);

            // Factory method for creation of listener instances.
            var factoryMethod =
                new Func<string, IResourceFacade, Fixture, IAdapterPlugin, IEventState, IObjectProvider<IUpdatableMarketStateCollection>, int, IListener>(
                    (sport, resource, fixtureSnapshot, connector, eventState,marketFilterObjectProvider, lastSequence) 
                        => new StreamListener(sport, resource, fixtureSnapshot, connector, eventState, marketFilterObjectProvider, lastSequence));

            Bind<Func<string, IResourceFacade, Fixture, IAdapterPlugin, IEventState, IObjectProvider<IUpdatableMarketStateCollection>, int, IListener>>().ToConstant(factoryMethod);
        }
    }
}

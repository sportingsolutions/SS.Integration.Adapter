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
using System.Collections.Generic;
using Ninject.Modules;
using SS.Integration.Adapter.Configuration;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.UdapiClient;
using SS.Integration.Adapter.UdapiClient.Model;

namespace SS.Integration.Adapter.WindowsService
{
    public class BootStrapper : NinjectModule
    {
        public override void Load()
        {
            Bind<ISettings>().To<Settings>().InSingletonScope();
            Bind<IReconnectStrategy>().To<DefaultReconnectStrategy>().InSingletonScope();
            Bind<IServiceFacade>().To<UdapiServiceFacade>();

            // Factory method for creation of listener instances.
            var factoryMethod =
                new Func<string, IResourceFacade, Fixture, IAdapterPlugin,IEventState,IObjectProvider<IDictionary<string,MarketState>>, int, IListener>(
                    (sport, resource, fixtureSnapshot, connector, eventState,marketFilterObjectProvider, lastSequence) 
                        => new StreamListener(sport, resource, fixtureSnapshot, connector, eventState, marketFilterObjectProvider, lastSequence));

            Bind<Func<string, IResourceFacade, Fixture, IAdapterPlugin, IEventState,IObjectProvider<IDictionary<string,MarketState>>, int, IListener>>().ToConstant(factoryMethod);
        }
    }
}

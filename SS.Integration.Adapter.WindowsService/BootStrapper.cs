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
using Ninject;
using SS.Integration.Adapter.Configuration;
using SS.Integration.Adapter.Diagnostics;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.UdapiClient;

namespace SS.Integration.Adapter.WindowsService
{
    public class BootStrapper : NinjectModule
    {
        #region Fields

        private readonly IAdapterPlugin _platformConnector;

        #endregion

        #region Constructors

        public BootStrapper(IAdapterPlugin platformConnector)
        {
            _platformConnector = platformConnector ?? throw new ArgumentNullException(nameof(platformConnector));
        }

        #endregion

        #region Public methods

        public override void Load()
        {
            Bind<ISettings>().To<Settings>().InSingletonScope();
            Bind<IReconnectStrategy>().To<DefaultReconnectStrategy>().InSingletonScope();
            Bind<IStateManager, IStateProvider>().To<StateManager>().InSingletonScope()
                .WithConstructorArgument("settings", Kernel.Get<ISettings>());
            Bind<IServiceFacade>().To<UdapiServiceFacade>();
            Bind<ISuspensionManager>().To<SuspensionManager>().InSingletonScope()
                .WithConstructorArgument("stateProvider", Kernel.Get<IStateProvider>())
                .WithConstructorArgument("plugin", _platformConnector);
            Bind<IStreamHealthCheckValidation>().To<StreamHealthCheckValidation>().InSingletonScope()
                .WithConstructorArgument("settings", Kernel.Get<ISettings>());
            Bind<IFixtureValidation>().To<FixtureValidation>().InSingletonScope();

            var supervisorStateManager = new SupervisorStateManager(Kernel.Get<ISettings>());
            Bind<IObjectProvider<Dictionary<string, FixtureOverview>>>().ToConstant(supervisorStateManager.StateProvider);
        }

        #endregion
    }
}

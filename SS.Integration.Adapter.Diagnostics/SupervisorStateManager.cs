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
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.Model.ProcessState;

namespace SS.Integration.Adapter.Diagnostics
{
    public class SupervisorStateManager 
    {
        private static IObjectProvider<Dictionary<string, FixtureOverview>> _stateProvider;

        public SupervisorStateManager(ISettings settings)
        {
            _stateProvider = 
                new BinaryStoreProvider<Dictionary<string, FixtureOverview>>(settings.SupervisorStatePath,
                    "Supervisor.bin");
        }

        public IObjectProvider<Dictionary<string, FixtureOverview>> StateProvider
        {
            get
            {
                return _stateProvider;
            }
        }
    }
}

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
using System.Linq;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Interface
{
    public interface IStreamListenerManager
    {
        bool HasStreamListener(string fixtureId);
        void StartStreaming(string fixtureId);
        void StopStreaming(string fixtureId);

        //event Adapter.StreamEventHandler StreamCreated;
        //event Adapter.StreamEventHandler StreamRemoved;

        IStateManager StateManager { get; set; }

        int ListenersCount { get; }
        void StopAll();
        
        /// <summary>
        /// This method indicates which fixtures are currently present in the feed
        /// for a given sport
        /// </summary>
        void UpdateCurrentlyAvailableFixtures(string sport, Dictionary<string, IResourceFacade> currentfixturesLookup);
        void CreateStreamListener(IResourceFacade resource, IAdapterPlugin platformConnector);
        bool RemoveStreamListener(string fixtureId);

        IEnumerable<IGrouping<string, IListener>> GetListenersBySport();
        bool ShouldProcessResource(IResourceFacade resource);
        bool TryLockProcessing(string fixtureId);
        void ReleaseProcessing(string fixtureId);

        Action<string> ProcessResourceHook { set; }
    }
}


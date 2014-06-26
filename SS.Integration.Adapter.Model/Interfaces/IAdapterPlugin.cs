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

namespace SS.Integration.Adapter.Model.Interfaces
{
    public interface IAdapterPlugin
    {
        /// <summary>
        /// Initialise the adapter's plug-in.
        /// This method will be called by the adapter
        /// when the plug-in is loaded
        /// </summary>
        void Initialise();

        /// <summary>
        /// The adapter calls this method
        /// when it retrieves a full snapshot.
        /// 
        /// It allows the plug-in to perform
        /// all the necessary business logic
        /// related to the acquisition of a
        /// new snasphot.
        /// 
        /// If an exception is raised, the adapter
        /// will try to get a new full snapshot.
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="hasEpochChanged">Indicates if the epoch field has changed since last snapshot</param>
        void ProcessSnapshot(Fixture fixture, bool hasEpochChanged = false);

        /// <summary>
        /// The adapter calls this method
        /// when it retrieves a delta snapshot.
        /// 
        /// It allows the plug-in to perform the 
        /// necessary business logic related
        /// to the acquisition of a new delta snapshot
        /// 
        /// If an exception is raised, the adapter
        /// will try to get a new full snapshot
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="hasEpochChanged"></param>
        void ProcessStreamUpdate(Fixture fixture, bool hasEpochChanged = false);

        /// <summary>
        /// Allows the plug-in to perform
        /// any business logic related to the
        /// change of the fixture's match status.
        /// </summary>
        /// <param name="fixture"></param>
        void ProcessMatchStatus(Fixture fixture);

        /// <summary>
        /// Allows the plug-in to perform
        /// any business logic related 
        /// the removal of a fixture.
        /// </summary>
        /// <param name="fixture"></param>
        void ProcessFixtureDeletion(Fixture fixture);

        /// <summary>
        /// Called by the adapter when it is 
        /// necessary to un-suspend a fixture
        /// </summary>
        /// <param name="fixture"></param>
        void UnSuspend(Fixture fixture);

        /// <summary>
        /// Called by the adapter when 
        /// it is necessary to suspend a fixture
        /// </summary>
        /// <param name="fixtureId"></param>
        void Suspend(string fixtureId);
        
        /// <summary>
        /// Called by the adapter during
        /// its disposal
        /// </summary>
        void Dispose();

        /// <summary>
        /// Defines a possibile empty set of
        /// plug-in specific market filtering rules
        /// </summary>
        IEnumerable<IMarketRule> MarketRules { get; }

    }
}

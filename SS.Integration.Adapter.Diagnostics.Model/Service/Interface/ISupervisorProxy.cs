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
using SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;

namespace SS.Integration.Adapter.Diagnostics.Model.Service.Interface
{
    public interface ISupervisorProxy : IDisposable
    {

        /// <summary>
        /// Returns a list of ISportOverview. One for each
        /// sport currently known by the adapter
        /// </summary>
        /// <returns></returns>
        IEnumerable<ISportOverview> GetSports();

        /// <summary>
        /// Returns a ISportDetail object for the given sport
        /// (it returns null if the sport doesn't exist).
        /// 
        /// ISportDetails is an ISportOverview but it also
        /// contains a list of IFixtureOverview (one for
        /// each fixture currently know by the adapter).
        /// 
        /// Fixture deleted or with MatchStatus=MatchOver are
        /// not included in the list.
        /// </summary>
        /// <param name="sportCode"></param>
        /// <returns></returns>
        ISportDetails GetSportDetail(string sportCode);

        /// <summary>
        /// Return an IFixtureDetails object for the given fixture
        /// (null if the fixtureId is invalid or the fixture doesn't exist)
        /// </summary>
        /// <param name="fixtureId"></param>
        /// <returns></returns>
        IFixtureDetails GetFixtureDetail(string fixtureId);

        /// <summary>
        /// Returns the IAdapterStatus object representing
        /// the adapter's status
        /// </summary>
        /// <returns></returns>
        IAdapterStatus GetAdapterStatus();

        IEnumerable<IFixtureProcessingEntry> GetFixtureHistory(string fixtureId);

        /// <summary>
        /// Returns all fixtures known by the adapter
        /// (deleted or matchover fixtures are not included)
        /// </summary>
        /// <returns></returns>
        IEnumerable<IFixtureOverview> GetFixtures();

        /// <summary>
        /// Forces the adapter in taking a new snasphot for
        /// the specified fixture
        /// </summary>
        /// <param name="fixtureId"></param>
        void TakeSnapshot(string fixtureId);

        /// <summary>
        /// Forces the adapter to restart the IListener object
        /// for the specified fixture
        /// </summary>
        /// <param name="fixtureId"></param>
        void RestartListener(string fixtureId);

        /// <summary>
        /// Forces the adapter to clear the state it is
        /// keeping for the specified fixture
        /// </summary>
        /// <param name="fixtureId"></param>
        void ClearState(string fixtureId);
    }
}

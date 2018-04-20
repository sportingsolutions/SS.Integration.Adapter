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

namespace SS.Integration.Adapter.Model.Interfaces
{
    public enum SuspensionReason
    {
        SUSPENSION,
        DISCONNECT_EVENT,
        FIXTURE_DELETED,
        FIXTURE_DISPOSING,
        FIXTURE_ERRORED,
        INTERNALERROR,
        SNAPSHOT,
        SDK_ERROR,
        HEALTH_CHECK_FALURE,
        MATCH_OVER,
        UPDTATE_DELAYED
    }


    public interface ISuspensionManager
    {
        #region Properties

        Action<IMarketStateCollection> DoNothingStrategy { get; }

        Action<IMarketStateCollection> SuspendFixtureStrategy { get; }

        Action<IMarketStateCollection> SuspendFixtureIfInPlayStrategy { get; }

        Action<IMarketStateCollection> SuspendAllMarketsStrategy { get; }

        Action<IMarketStateCollection> SuspendInPlayMarketsStrategy { get; }

        Action<IMarketStateCollection> SuspendAllMarketsAndSetMatchStatusDeleted { get; }

        Action<IMarketStateCollection> SuspendFixtureAndSetMatchStatusDeleted { get; }

        #endregion

        #region Methods

        void RegisterAction(Action<IMarketStateCollection> action, SuspensionReason reason);

        void Suspend(Fixture fixture, SuspensionReason reason);

        void Unsuspend(Fixture fixture);

        #endregion
    }
}

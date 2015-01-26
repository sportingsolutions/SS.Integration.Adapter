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
        FIXTURE_ERRORED
    }

    public interface ISuspensionManager
    {
        Action<IMarketStateCollection> DoNothingStrategy { get; }

        Action<IMarketStateCollection> SuspendFixtureStrategy { get; } 

        Action<IMarketStateCollection> SuspendFixtureIfInPlayStrategy { get; }
        
        Action<IMarketStateCollection> SuspendAllMarketsStrategy { get; }

        Action<IMarketStateCollection> SuspendInPlayMarketsStrategy { get; }

        Action<IMarketStateCollection> SuspendAllMarketsAndSetMatchStatusDeleted { get; }
        
        Action<IMarketStateCollection> SuspendFixtureAndSetMatchStatusDeleted { get; }

        void RegisterAction(Action<IMarketStateCollection> action, SuspensionReason reason);

        void Suspend(string fixtureId, SuspensionReason reason);

        void Unsuspend(string fixutreId);
    }
}

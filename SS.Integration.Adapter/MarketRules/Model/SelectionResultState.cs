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
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules.Model
{
    [Serializable]
    public class SelectionResultState : IUpdatableSelectionResultState
    {
        public SelectionResultState()
        {
        }

        public SelectionResultState(Result result)
            : this()
        {
            Update(result);
        }

        public int WinParticipants { get; private set; }

        public int StakeParticipants { get; private set; }
        
        public int WinPlaces { get; private set; }
        
        public int StakePlaces { get; private set; }
        
        public bool IsEqualTo(ISelectionResultState result)
        {
            if (result == null)
                return false;

            if (ReferenceEquals(this, result))
                return true;

            return this.WinParticipants == result.WinParticipants &&
                   this.StakeParticipants == result.StakeParticipants &&
                   this.StakePlaces == result.StakePlaces &&
                   this.WinPlaces == result.WinPlaces;
        }

        public bool IsEquivalentTo(Result result)
        {
            if (result == null)
                return false;

            return this.WinParticipants == result.WinParticipants &&
                   this.StakeParticipants == result.StakeParticipants &&
                   this.StakePlaces == result.StakePlaces &&
                   this.WinPlaces == result.WinPlaces;
        }

        public void Update(Result result)
        {
            WinParticipants = result.WinParticipants;
            StakeParticipants = result.StakeParticipants;
            WinPlaces = result.WinPlaces;
            StakePlaces = result.StakePlaces;
        }

        public IUpdatableSelectionResultState Clone()
        {
            return new SelectionResultState
            {
                WinPlaces = this.WinPlaces,
                StakeParticipants = this.StakeParticipants,
                WinParticipants = this.WinParticipants,
                StakePlaces = this.StakePlaces
            };
        }
    }
}

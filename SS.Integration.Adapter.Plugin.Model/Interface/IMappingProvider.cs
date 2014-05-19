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

using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Plugin.Model.Interface
{
    //TODO: Move this interface to adapter and it's data model to adaptor
    public interface IMappingProvider
    {
        MarketMapping GetMarketMapping(string marketId);
        SelectionMapping GetSelectionMapping(string selectionId);
        void IncrementIndex(string marketId);
        void UpdateHandicapMarkets(Fixture fixture, Mapping mapping);
        void AddMappingsForNewMarkets(Fixture snapshot, Mapping mapping);
        void RefreshMappings(Fixture snapshot, string[] marketIds, Mapping mapping);
        bool IsHandicapLineShiftedUp(Fixture fixture);
        string HandicapLineIndicatorMarketId { get; }
    }
}

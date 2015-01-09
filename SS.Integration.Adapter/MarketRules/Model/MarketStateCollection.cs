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
using System.Threading.Tasks;
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Extensions;

namespace SS.Integration.Adapter.MarketRules.Model
{
    [Serializable]
    internal class MarketStateCollection : IUpdatableMarketStateCollection
    {
        private readonly Dictionary<string, IUpdatableMarketState> _States;

        private MarketStateCollection()
        {
            _States = new Dictionary<string, IUpdatableMarketState>();
        }

        public MarketStateCollection(string fixtureId)
            : this()
        {
            FixtureId = fixtureId;
        }

        public MarketStateCollection(string fixtureId, IUpdatableMarketStateCollection collection)
            : this(fixtureId)
        {

            if (collection == null)
                return;

            Sport = collection.Sport;

            // this is just for creating the nodes
            foreach (var mkt_id in collection.Markets)
            {
                _States[mkt_id] = null;

            }

            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            // fill the nodes and as they are already created, there is no race-condition here
            Parallel.ForEach(collection.Markets, options, x =>
                {
                    _States[x] = ((IUpdatableMarketState)collection[x]).Clone();
                }
            );
        }

        #region IMarketStateCollection

        public string FixtureId { get; private set; }

        public MatchStatus FixtureStatus { get; private set; }

        public int FixtureSequence { get; private set; }

        public string FixtureName { get; private set; }

        public string Sport { get; private set; }

        public bool HasMarket(string MarketId)
        {
            return _States.ContainsKey(MarketId);
        }

        public IMarketState this[string MarketId]
        {
            get
            {
                if (string.IsNullOrEmpty(MarketId))
                    throw new ArgumentNullException("MarketId");

                return HasMarket(MarketId) ? _States[MarketId] : null;
            }
            internal set
            {
                if (!string.IsNullOrEmpty(MarketId) && value != null)
                    _States[MarketId] = value as IUpdatableMarketState;
            }
        }

        public IEnumerable<string> Markets
        {
            get { return _States.Keys; }
        }

        public int MarketCount
        {
            get { return _States.Count; }
        }

        #endregion

        #region IUpdatableMarketStateCollection

        public void Update(Fixture fixture, bool fullSnapshot)
        {
            FixtureSequence = fixture.Sequence;
            FixtureName = fixture.FixtureName;
            FixtureStatus = (MatchStatus) Enum.Parse(typeof (MatchStatus), fixture.MatchStatus);

            if (fullSnapshot)
            {
                if(fixture.Tags.ContainsKey("Sport"))
                    Sport = fixture.Tags["Sport"].ToString();

                var marketsLookUp = fixture.Markets.ToDictionary(m => m.Id);

                //if market doesn't exist in the snapshot the definition must have been changed
                Markets.Where(marketId => !marketsLookUp.ContainsKey(marketId)).ForEach(marketId => this[marketId].IsDeleted = true);
            }

            foreach (var market in fixture.Markets)
            {
                IUpdatableMarketState mkt_state = null;
                if (HasMarket(market.Id))
                {
                    mkt_state = this[market.Id] as IUpdatableMarketState;
                    mkt_state.Update(market, fullSnapshot);
                }
                else
                {
                    mkt_state = new MarketState(market, fullSnapshot);
                    this[market.Id] = mkt_state;
                }
            }
        }

        public void OnMarketsForcedSuspension(IEnumerable<IMarketState> markets)
        {
            SetForcedSuspensionOnState(markets,true);
        }

        public void OnMarketsForcedUnsuspension(IEnumerable<IMarketState> markets)
        {
            SetForcedSuspensionOnState(markets,false);
        }

        private void SetForcedSuspensionOnState(IEnumerable<IMarketState> markets,bool isForceSuspended)
        {
            if (markets == null)
                return;

            foreach (var mkt in markets)
            {
                _States[mkt.Id].SetForcedSuspensionState(isForceSuspended);
            }
        }

        #endregion
    }
}

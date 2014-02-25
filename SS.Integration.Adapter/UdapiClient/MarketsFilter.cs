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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.UdapiClient.Model;
using SS.Integration.Common;
using log4net;

namespace SS.Integration.Adapter.UdapiClient
{
    /// <summary>
    /// Not Thread-Safe class
    /// Keeps the list of inactive markets for a specific fixture so they can be filtered out 
    /// in order to avoid updating inactive mappings more than once
    /// </summary>
    public class MarketsFilter
    {
        //private readonly IObjectProvider<IDictionary<string, MarketState>> _storeProvider;
        private readonly ILog _logger = LogManager.GetLogger(typeof(MarketsFilter).ToString());

        private readonly string _fixtureId;

        private readonly IObjectProvider<IDictionary<string, MarketState>> _marketStates;
        private readonly string _storagePath;
        
        private readonly ConcurrentDictionary<string,IDictionary<string,MarketState>> _ongoingTransactions = new ConcurrentDictionary<string, IDictionary<string, MarketState>>();

        public MarketsFilter(Fixture fixture, IObjectProvider<IDictionary<string, MarketState>> storeProvider = null)
        {
            _logger.DebugFormat("Initiating market filters for {0}", fixture);
            _fixtureId = fixture.Id;
            _storagePath = string.Format("FilteredMarkets-{0}.bin", fixture.Id);

            //var storedState = _storeProvider != null ? _storeProvider.GetObject(_storagePath) : null;
            _marketStates = storeProvider;
            
            if (_marketStates.GetObject(_fixtureId) == null)
            {
                _marketStates.SetObject(_fixtureId, new Dictionary<string, MarketState>());
            }

            var marketStates = _marketStates.GetObject(_fixtureId);
            foreach (var market in fixture.Markets)
            {
                if (marketStates.ContainsKey(market.Id))
                    marketStates[market.Id].UpdateObject(market);
                else
                {
                    var marketState = new MarketState(market);
                    marketStates[marketState.Id] = marketState;
                }

                market.IsSuspended = marketStates[market.Id].IsSuspended;
            }

            UpdateMarketStateInStorage(marketStates);

            _logger.DebugFormat("Market filters initiated successfully for {0}", fixture);
        }

        private void UpdateMarketStateInStorage(IDictionary<string, MarketState> marketStates)
        {
            _marketStates.SetObject(_fixtureId, marketStates);
        }

        /// <summary>
        /// Filter out inactive markets from snapshot/delta
        /// </summary>
        public void FilterInactiveMarkets(Fixture fixture)
        {
            _logger.DebugFormat("Filtering inactive markets for {0}", fixture);

            if (fixture.Id != _fixtureId)
            {
                throw new ArgumentException(string.Format("The collection of inactive markets has been created for {0}. You cannot pass in argument fixture id with value {1}", fixture, fixture.Id));
            }

            RemoveInactiveMarketsFromFixture(fixture);
            
            // this is to prevent modifying in memory copy
            var marketStates = new Dictionary<string, MarketState>(_marketStates.GetObject(_fixtureId));

            foreach (var market in fixture.Markets)
            {
                if(marketStates.ContainsKey(market.Id))
                {
                    var copyMarketState = marketStates[market.Id].Clone() as MarketState;

                    copyMarketState.UpdateObject(market);
                    market.IsSuspended = copyMarketState.IsSuspended;
                    marketStates[copyMarketState.Id] = copyMarketState;
                }
                else
                {
                    var marketState = new MarketState(market);
                    marketStates[marketState.Id] = marketState;
                }
            }

            StartCacheTransaction(marketStates);
        }

        public void CommitChanges()
        {
            if (_ongoingTransactions.ContainsKey(_fixtureId))
            {
                UpdateMarketStateInStorage(_ongoingTransactions[_fixtureId]);
            }
        }

        public void RollbackChanges()
        {
            IDictionary<string, MarketState> useless; 
            _ongoingTransactions.TryRemove(_fixtureId,out useless);
        }

        private void StartCacheTransaction(IDictionary<string, MarketState> marketStates)
        {
            _ongoingTransactions.AddOrUpdate(_fixtureId,marketStates,(a,b) => marketStates);
        }


        /// <summary>
        /// Remove markets from snapshot/update when: 
        /// current market state is inactive AND the previous state was inactive too (with no change in status or name)
        /// 
        /// NB: If there is a change in market's name or status then will not be removed
        /// </summary>
        private void RemoveInactiveMarketsFromFixture(Fixture fixture)
        {
            if (fixture.Id != _fixtureId)
            {
                throw new ArgumentException(string.Format("The collection of pending markets has been created for fixture id {0}. You cannot pass in argument fixture id with value {1}", _fixtureId, fixture.Id));
            }

            var marketStatesDictionary = _marketStates.GetObject(_fixtureId);

            var inactiveMarkets = fixture.Markets.Where(m => marketStatesDictionary.ContainsKey(m.Id) && !marketStatesDictionary[m.Id].IsActive);

            // The collection of Fixture.Markets is not parallelizable as it's not thread-safe
            foreach (var market in inactiveMarkets.ToArray())
            {
                if (!marketStatesDictionary.ContainsKey(market.Id))
                    continue;

                var marketState = marketStatesDictionary[market.Id];

                // Only remove market from snapshot/delta if it is not active AND values like name and status have not changed
                if (marketState.IsEqualsTo(market))
                {
                    fixture.Markets.Remove(market);

                    _logger.InfoFormat("Update will not be sent for {0} in fixtureId={1} as it is not active and got same values (name and status)", market,_fixtureId);
                }

            }
        }

        public void VoidUnsettled(Fixture fixture)
        {
            if (!fixture.IsMatchOver)
            {
                return;
            }

            var fullState = _marketStates.GetObject(fixture.Id);

            var markets = fixture.Markets.ToDictionary(m => m.Id);

            // get list of markets which are either no longer in snapshot or are in the snpashot and are not resulted
            // markets which were already priced (activated) should be ignored
            var marketsNotPresentInTheSnapshot = fullState.Values.Where(
                marketState => !marketState.IsResulted
                    && (!markets.ContainsKey(marketState.Id) || !markets[marketState.Id].IsResulted))
                .Select(mState => mState.Id);

            if (!marketsNotPresentInTheSnapshot.Any())
                return;

            foreach (var marketId in marketsNotPresentInTheSnapshot)
            {
                var marketState = fullState[marketId];
                
                var market = fixture.Markets.FirstOrDefault(m => m.Id == marketId);
                if (marketState.IsActivated)
                {
                    _logger.WarnFormat("Market was priced during the fixture lifetime but has NOT been settled on match over. marketId={0} {1}",marketId,fixture);
                    continue;
                }

                if (market == null)
                {
                    _logger.DebugFormat(
                        "Market will be voided using auto voiding as it was missing in the snapshot, marketId={0} {1}",
                        marketId, fixture);
                    fixture.Markets.Add(CreateSettledMarket(fullState[marketId]));
                }
                else
                {
                    _logger.WarnFormat(
                        "Voiding market that was in the snapshot but wasn't resulted. marketId={0} {1}",
                        market.Id, fixture);
                    market.Selections.ForEach(s => s.Status = "3");
                }

            }


        }

        private Market CreateSettledMarket(MarketState marketState)
        {
            var market = new Market { Id = marketState.Id };

            if (marketState.Line != null)
            {
                market.Tags.Add("line", marketState.Line);
            }

            market.Selections.AddRange(
                marketState.GetSelectionIds().Select(sId => new Selection { Id = sId, Status = "3", Price = 0 }));

            return market;
        }

        private Market CreateSuspendedMarket(MarketState marketState)
        {
            var market = new Market { Id = marketState.Id, IsSuspended = true };
            market.Selections.AddRange(
                marketState.GetSelectionIds().Select(sId => new Selection { Id = sId, Tradable = false }));

            return market;
        }

        public void Clear()
        {
            _marketStates.Remove(_fixtureId);
        }

        public Fixture GenerateAllMarketsSuspenssion(string fixtureId)
        {
            var marketStates = _marketStates.GetObject(fixtureId);
            var fixture = new Fixture() { Id = fixtureId, MatchStatus = ((int) MatchStatus.Ready).ToString()};
            fixture.Markets.AddRange(marketStates.Select(kv => CreateSuspendedMarket(kv.Value)));

            return fixture;
        }
    }
}

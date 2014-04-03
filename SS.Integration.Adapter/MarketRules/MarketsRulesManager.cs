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
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using log4net;

namespace SS.Integration.Adapter.MarketRules
{
    /// <summary>
    /// Not Thread-Safe class
    /// Keeps the list of inactive markets for a specific fixture so they can be filtered out 
    /// in order to avoid updating inactive mappings more than once
    /// </summary>
    public class MarketsRulesManager
    {        
        private readonly ILog _logger = LogManager.GetLogger(typeof(MarketsRulesManager).ToString());

        private readonly string _FixtureId;
        private readonly IObjectProvider<IMarketStateCollection> _StateProvider;
        private readonly IEnumerable<IMarketRule> _Rules;
        private IMarketStateCollection _CurrentTransaction;
        

        public MarketsRulesManager(Fixture fixture, IObjectProvider<IMarketStateCollection> StateProvider, IEnumerable<IMarketRule> FilteringRules)
        {
            _logger.DebugFormat("Initiating market filters for {0}", fixture);
            _FixtureId = fixture.Id;
            _Rules = FilteringRules;

            _StateProvider = StateProvider;

            var state = _StateProvider.GetObject(_FixtureId) ?? new MarketStateCollection();

            foreach (var market in fixture.Markets)
            {
                if (state.HasMarket(market.Id))
                    state[market.Id].Update(market);
                else
                {
                    var marketState = new MarketState(market);
                    state[marketState.Id] = marketState;
                }

                market.IsSuspended = state[market.Id].IsSuspended;
            }

            _StateProvider.SetObject(_FixtureId, state);

            _logger.DebugFormat("Market filters initiated successfully for {0}", fixture);
        }

        public void CommitChanges()
        {
            lock (this)
            {
                if (_CurrentTransaction == null)
                    return;

                _StateProvider.SetObject(_FixtureId, _CurrentTransaction);
                _CurrentTransaction = null;

            }
        }

        public void RollbackChanges()
        {
            lock (this)
            {
                _CurrentTransaction = null;
            }
        }

        private IMarketStateCollection BeginTransaction()
        {
            var clone = new MarketStateCollection();
            var state = _StateProvider.GetObject(_FixtureId);

            foreach (var mkt_id in state.Markets)
            {
                clone[mkt_id] = state[mkt_id].Clone();
            }
            
            lock (this)
            {
                _CurrentTransaction = clone;
            }

            return clone;
        }

        public void ApplyRules(Fixture Fixture)
        {
            if (Fixture == null)
                throw new ArgumentNullException("Fixture");

            if (Fixture.Id != _FixtureId)
            {
                throw new ArgumentException("MarketFilter has been created for fixtureId=" + _FixtureId + ". " +
                    "You cannot pass in fixtureId=" + Fixture.Id);
            }

            var state = BeginTransaction();

            foreach (var rule in _Rules)
            {
                if(rule == null)
                    continue;

                _logger.DebugFormat("Filtering markets with rule={0}", rule.Name);
                rule.Apply(Fixture, state);
                _logger.DebugFormat("Filtering market with rule={0} completed", rule.Name);
            }
        }

        private static Market CreateSuspendedMarket(IMarketState marketState)
        {
            var market = new Market { Id = marketState.Id, IsSuspended = true };
            foreach (var seln in marketState.Selections)
                market.Selections.Add(new Selection { Id = seln.Id, Tradable = false });

            return market;
        }

        public void Clear()
        {
            _StateProvider.Remove(_FixtureId);
        }

        public Fixture GenerateAllMarketsSuspenssion()
        {
            var marketStates = _StateProvider.GetObject(_FixtureId);
            var fixture = new Fixture { Id = _FixtureId, MatchStatus = ((int)MatchStatus.Ready).ToString() };

            foreach(var mkt_id in marketStates.Markets)
                fixture.Markets.Add(CreateSuspendedMarket(marketStates[mkt_id]));

            return fixture;
        }
         
    }
}

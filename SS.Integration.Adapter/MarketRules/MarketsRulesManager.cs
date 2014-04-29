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
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using log4net;

namespace SS.Integration.Adapter.MarketRules
{
   
    public class MarketsRulesManager
    {        
        private readonly ILog _logger = LogManager.GetLogger(typeof(MarketsRulesManager).ToString());

        private readonly string _FixtureId;
        private readonly IObjectProvider<IUpdatableMarketStateCollection> _StateProvider;
        private readonly IEnumerable<IMarketRule> _Rules;
        private IUpdatableMarketStateCollection _CurrentTransaction;


        internal MarketsRulesManager(Fixture fixture, IObjectProvider<IUpdatableMarketStateCollection> StateProvider, IEnumerable<IMarketRule> FilteringRules)
        {
            _logger.DebugFormat("Initiating market rule manager for {0}", fixture);
            
            _FixtureId = fixture.Id;
            _Rules = FilteringRules;

            _StateProvider = StateProvider;

            var state = _StateProvider.GetObject(_FixtureId) ?? new MarketStateCollection();
            state.Update(fixture, true);
            _StateProvider.SetObject(_FixtureId, state);

            _logger.DebugFormat("Market rule manager initiated successfully for {0}", fixture);
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

        private IMarketStateCollection BeginTransaction(IUpdatableMarketStateCollection OldState, Fixture Fixture)
        {
           
            // get a new market state by cloning the previous one
            // and then updating it with the new info coming within
            // the snapshot
            var clone = new MarketStateCollection(OldState);
            clone.Update(Fixture, Fixture.Tags != null && Fixture.Tags.Any());
               
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
                throw new ArgumentException("MarketsRulesManager has been created for fixtureId=" + _FixtureId + 
                    " You cannot pass in fixtureId=" + Fixture.Id);
            }

            var oldstate = _StateProvider.GetObject(Fixture.Id);
            var newstate = BeginTransaction(oldstate, Fixture);

            ParallelOptions options = new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount};

            // we create a temp dictionary so we can apply the rules in parallel without accessing (writing to) any 
            // shared variables
            Dictionary<IMarketRule, IMarketRuleResultIntent> tmp = new Dictionary<IMarketRule,IMarketRuleResultIntent>();
            foreach(var rule in _Rules)
                tmp[rule] = null;

            Parallel.ForEach(tmp.Keys, options, rule =>
                {
                    _logger.DebugFormat("Filtering markets with rule={0}", rule.Name);
                    var result = tmp[rule];
                    rule.Apply(Fixture, oldstate, newstate, out result);
                    _logger.DebugFormat("Filtering market with rule={0} completed", rule.Name);
                }
            );
        }

        private static Market CreateSuspendedMarket(IMarketState marketState)
        {
            var market = new Market (marketState.Id) { IsSuspended = true };
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

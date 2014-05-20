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


        internal MarketsRulesManager(Fixture fixture, IObjectProvider<IUpdatableMarketStateCollection> stateProvider, IEnumerable<IMarketRule> filteringRules)
        {
            _logger.DebugFormat("Initiating market rule manager for {0}", fixture);
            
            _FixtureId = fixture.Id;
            _Rules = filteringRules;

            _StateProvider = stateProvider;

            _logger.DebugFormat("Market rule manager initiated successfully for {0}", fixture);

        }

        public void CommitChanges()
        {
            if (_CurrentTransaction == null)
                return;

            _StateProvider.SetObject(_FixtureId, _CurrentTransaction);
            _CurrentTransaction = null;
        }

        public void RollbackChanges()
        {
            _CurrentTransaction = null;
        }

        private void BeginTransaction(IUpdatableMarketStateCollection oldState, Fixture fixture)
        {
           
            // get a new market state by cloning the previous one
            // and then updating it with the new info coming within
            // the snapshot

            var clone = new MarketStateCollection(oldState);
            clone.Update(fixture, fixture.Tags != null && fixture.Tags.Any());
               
            _CurrentTransaction = clone;
        }

        /// <summary>
        /// Allows to apply the registered rules to the given fixture.
        /// 
        /// A rule can edit/remove/add markets as it wish (through their
        /// intents)
        /// 
        /// However, as there might be multiple rules, conflicts usually
        /// arise. When there is a conflict, the MarketRuleManager
        /// always takes a conservative approach.
        /// 
        /// </summary>
        /// <param name="fixture"></param>
        public void ApplyRules(Fixture fixture)
        {
            if (fixture == null)
                throw new ArgumentNullException("fixture");

            if (fixture.Id != _FixtureId)
            {
                throw new ArgumentException("MarketsRulesManager has been created for fixtureId=" + _FixtureId +
                    " You cannot pass in fixtureId=" + fixture.Id);
            }

            var oldstate = _StateProvider.GetObject(fixture.Id);
            BeginTransaction(oldstate, fixture);

            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount};

            // we create a temp dictionary so we can apply the rules in parallel 
            // without accessing (writing to) any shared variables
            Dictionary<IMarketRule, IMarketRuleResultIntent> tmp = new Dictionary<IMarketRule, IMarketRuleResultIntent>();
            foreach (var rule in _Rules)
                tmp[rule] = null;


            // a rule usually goes through the entire list of markets...if the fixture
            // comes from a full snapshot, it can have a lot of markets...
            Parallel.ForEach(tmp.Keys.ToList(), options, rule =>
                {                    
                    _logger.DebugFormat("Filtering markets with rule={0}", rule.Name);
                    tmp[rule] = rule.Apply(fixture, oldstate, _CurrentTransaction);
                    _logger.DebugFormat("Filtering market with rule={0} completed", rule.Name);
                }
            );


            MergeIntents(fixture, tmp);
        }

        /// <summary>
        /// E = Editable , !E = NotEditable
        /// R = Removable, !R = NotRemovable
        ///        
        /// This table shows how conflicts are solved 
        /// on a specific market
        ///
        /// (Rule 1, Rule 2) => Result
        /// (E, R)  => E
        /// (E, !R) => E
        /// (E, E1) => E + E1
        /// (E, !E) => !E    
        /// 
        /// (R, !R) => !R
        /// (R, R)  => R
        /// (R, !E) => R
        /// 
        /// (!R, !R) => !R
        /// (!R, !E) => !E + !R
        /// 
        /// (!E, !E) => !E
        /// 
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="intents"></param>
        private void MergeIntents(Fixture fixture, Dictionary<IMarketRule, IMarketRuleResultIntent> intents)
        {
            Dictionary<Market, bool> toremove = new Dictionary<Market, bool>();
            List<Market> toadd = new List<Market>();
            Dictionary<Market, Dictionary<Action<Market>, string>> toedit = new Dictionary<Market, Dictionary<Action<Market>, string>>();

            foreach (var rule in intents.Keys)
            {
                var intent = intents[rule];

                /* "toremove" lists all the markets that a rule wants to remove.
                 * Those that would actually be removed are only those
                 * whose flag is set to true. Those that have the flag
                 * set to false are markets that some rule wanted to 
                 * remove but some other rule marked them as not
                 * removable. As we follow a conservative approch
                 * we only remove a market if no other rule specified
                 * otherwise.
                 */

                foreach (var mkt in intent.MarkedAsRemovable)
                {
                    // if it already contains the market, don't do 
                    // anything as the flag could be "false"
                    if (!toremove.ContainsKey(mkt)) 
                        toremove.Add(mkt, true);
                }

                foreach (var mkt in intent.MarkedAsUnRemovable)
                {
                    // if it is already present, then 
                    // set its flag to false
                    if (toremove.ContainsKey(mkt))
                        toremove[mkt] = false;
                    else
                        toremove.Add(mkt, false);
                }

                /* For "editable" markets we follow the same 
                 * reasoning we do for removing the markets,
                 * except here the flag is the action to perform
                 * or null if a rule marked the market to be
                 * not-editable
                 */

                foreach (var mkt in intent.Edited)
                {
                    if (!toedit.ContainsKey(mkt))
                    {
                        toedit.Add(mkt, new Dictionary<Action<Market>, string> {{intent.GetEditingAction(mkt), rule.Name}});
                    }
                    else if (toedit[mkt] != null)
                    {
                        toedit[mkt].Add(intent.GetEditingAction(mkt), rule.Name);
                    }
                }

                foreach (var mkt in intent.MarkedAsUnEditable)
                {
                    if (toedit.ContainsKey(mkt))
                        toedit[mkt] = null;
                    else
                        toedit.Add(mkt, null);
                }

                toadd.AddRange(intent.Added);
            }


            toadd.ForEach(x => _logger.DebugFormat("Adding market {0} to {1} as requested by market rules", x, fixture));
            fixture.Markets.AddRange(toadd);

            foreach (var mkt in toedit.Keys)
            {
                if (toedit[mkt] != null)
                {
                    try
                    {
                        var count = 0;
                        var total = toedit[mkt].Count;
                        foreach (var action in toedit[mkt].Keys)
                        {
                            count++;

                            if(action == null)
                                continue;

                            _logger.DebugFormat("Performing edit action={0} count={1}/{2} on {3} of {4} as requested by market rules", 
                                toedit[mkt][action], count, total, mkt, fixture);
                            action(mkt);
                        }

                        _logger.DebugFormat("Successfully applied edit actions on {0} of {1} as requested by market rules", mkt, fixture);

                        // as we might have changed import details of the market, we need to update the market state
                        ((IUpdatableMarketState)_CurrentTransaction[mkt.Id]).Update(mkt, false);

                        _logger.DebugFormat("Updating market state for {0} of {1}", mkt, fixture);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(string.Format("An error occured while executing an edit action as requested by market rules on {0} of {1}", mkt, fixture), e);

                        throw;
                    }
                }
            }

            foreach (var mkt in toremove.Keys)
            {
                // we need to check that a removable market
                // wasn't marked as editable or not editable
                if (toremove[mkt] && !toedit.ContainsKey(mkt))
                {
                    _logger.DebugFormat("{0} of {1} will be removed from snapshot due market rules", mkt, fixture);
                    fixture.Markets.Remove(mkt);
                }
            }
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
            var fixture = new Fixture { Id = _FixtureId, MatchStatus = ((int)MatchStatus.Ready).ToString() };

            if (_CurrentTransaction == null)
                return fixture;

            foreach (var mkt_id in _CurrentTransaction.Markets)
                fixture.Markets.Add(CreateSuspendedMarket(_CurrentTransaction[mkt_id]));

            return fixture;
        }
         
    }
}

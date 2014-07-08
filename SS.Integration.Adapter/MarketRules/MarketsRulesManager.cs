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
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using log4net;

namespace SS.Integration.Adapter.MarketRules
{
   
    public class MarketRulesManager : IMarketRulesManager
    {        
        private readonly ILog _logger = LogManager.GetLogger(typeof(MarketRulesManager).ToString());

        private readonly string _fixtureId;
        private readonly IStoredObjectProvider _stateProvider;
        private readonly IEnumerable<IMarketRule> _rules;
        private IUpdatableMarketStateCollection _currentTransaction;


        internal MarketRulesManager(string fixtureId, IStoredObjectProvider stateProvider, IEnumerable<IMarketRule> filteringRules)
        {
            _logger.DebugFormat("Initiating market rule manager for fixtureId={0}", fixtureId);
            
            _fixtureId = fixtureId;
            _rules = filteringRules;

            _stateProvider = stateProvider;

            _logger.DebugFormat("Market rule manager initiated successfully for fixtureId={0}", fixtureId);
        }

        #region IMarketRulesManager

        public void CommitChanges()
        {
            if (_currentTransaction == null)
                return;

            _stateProvider.SetObject(_fixtureId, _currentTransaction);
            _currentTransaction = null;
        }

        public void ApplyRules(Fixture fixture)
        {
            if (fixture == null)
                throw new ArgumentNullException("fixture");

            if (fixture.Id != _fixtureId)
            {
                throw new ArgumentException("MarketsRulesManager has been created for fixtureId=" + _fixtureId +
                    " You cannot pass in fixtureId=" + fixture.Id);
            }

            var oldstate = _stateProvider.GetObject(fixture.Id);
            BeginTransaction(oldstate, fixture);

            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

            // we create a temp dictionary so we can apply the rules in parallel 
            // without accessing (writing to) any shared variables
            Dictionary<IMarketRule, IMarketRuleResultIntent> tmp = new Dictionary<IMarketRule, IMarketRuleResultIntent>();
            foreach (var rule in _rules)
                tmp[rule] = null;


            // a rule usually goes through the entire list of markets...if the fixture
            // comes from a full snapshot, it can have a lot of markets...
            Parallel.ForEach(tmp.Keys.ToList(), options, rule =>
                {                    
                    _logger.DebugFormat("Filtering markets with rule={0}", rule.Name);
                    tmp[rule] = rule.Apply(fixture, oldstate, _currentTransaction);
                    _logger.DebugFormat("Filtering market with rule={0} completed", rule.Name);
                }
            );


            MergeIntents(fixture, tmp);
        }

        public Fixture GenerateAllMarketsSuspenssion(int sequence = -1)
        {
            var fixture = new Fixture { Id = _fixtureId, MatchStatus = ((int)MatchStatus.Ready).ToString(), Sequence = sequence };

            if (CurrentState == null)
                return fixture;
            
            foreach (var mkt_id in CurrentState.Markets)
            {
                fixture.Markets.Add(CreateSuspendedMarket(CurrentState[mkt_id]));
            }

            return fixture;
        }
        
        public IMarketStateCollection CurrentState
        {
            get { return _currentTransaction ?? _stateProvider.GetObject(_fixtureId); }
        }

        public void RollbackChanges()
        {
            _currentTransaction = null;
        }

        #endregion

        private void BeginTransaction(IUpdatableMarketStateCollection oldState, Fixture fixture)
        {
           
            // get a new market state by cloning the previous one
            // and then updating it with the new info coming within
            // the snapshot

            var clone = new MarketStateCollection(oldState);
            clone.Update(fixture, fixture.Tags != null && fixture.Tags.Any());
               
            _currentTransaction = clone;
        }

        /// <summary>
        /// Intents:
        /// 
        /// E = Editable , !E = NotEditable
        /// R = Removable, !R = NotRemovable
        ///        
        /// This table shows how conflicts are solved 
        /// on a specific market
        ///
        /// (Rule 1, Rule 2) => Result
        /// (E, R)  => E
        /// (E, !R) => E
        /// (E, E1) => See below
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
        /// When there are more than one rule that want to edit a specific market, the 
        /// MarketRuleEditIntent.OperationType is considered.
        /// 
        /// OperationType can be: CHANGE_SELECTIONS (CS), ADD_SELECTIONS (AS), REMOVE_SELECTIONS (RS), CHANGE_DATA (CD)
        /// 
        /// (CS, AS) => CS + AS (changing operation will be perfomed on the existing selections. Newly added selections will not be edited)
        /// (CS, RS) => CS
        /// (CS, CS) => CS + CS (this might cause unexpected results)
        /// (CS, CD) => CS + CD
        /// 
        /// (AS, RS) => RS + AS (existing selection will be removed and only after that the new one are added)
        /// (AS, CD) => AS + CD
        /// (AS, AS) => AS + AS
        /// 
        /// (CD, CD) => CD + CD (this might cause unexpected results)
        /// (CD, RS) => CD + RS
        /// 
        /// (RS, RS) => RS + RS
        /// </summary>
        /// <param name="fixture"></param>
        /// <param name="intents"></param>
        private void MergeIntents(Fixture fixture, Dictionary<IMarketRule, IMarketRuleResultIntent> intents)
        {
            Dictionary<Market, bool> toremove = new Dictionary<Market, bool>();
            List<Market> toadd = new List<Market>();
            Dictionary<Market, Dictionary<MarketRuleEditIntent, string>> toedit = new Dictionary<Market, Dictionary<MarketRuleEditIntent, string>>();

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

                foreach (var mkt in intent.RemovableMarkets)
                {
                    // if it already contains the market, don't do 
                    // anything as the flag could be "false"
                    if (!toremove.ContainsKey(mkt)) 
                        toremove.Add(mkt, true);
                }

                foreach (var mkt in intent.UnRemovableMarkets)
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

                foreach (var mkt in intent.EditedMarkets)
                {
                    if (!toedit.ContainsKey(mkt))
                    {
                        toedit.Add(mkt, new Dictionary<MarketRuleEditIntent, string> { { intent.GetEditingAction(mkt), rule.Name } });
                    }
                    else if (toedit[mkt] != null)
                    {
                        toedit[mkt].Add(intent.GetEditingAction(mkt), rule.Name);
                    }
                }

                foreach (var mkt in intent.UnEditableMarkets)
                {
                    if (toedit.ContainsKey(mkt))
                        toedit[mkt] = null;
                    else
                        toedit.Add(mkt, null);
                }

                toadd.AddRange(intent.NewMarkets);
            }

            // ADD
            toadd.ForEach(x => _logger.DebugFormat("Adding market {0} to {1} as requested by market rules", x, fixture));
            fixture.Markets.AddRange(toadd);

            // EDIT
            MergeEditIntents(fixture, toedit);
            
            // REMOVE
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

        private void MergeEditIntents(Fixture fixture, Dictionary<Market, Dictionary<MarketRuleEditIntent, string>> toedit)
        {
            foreach (var mkt in toedit.Keys)
            {
                if (toedit[mkt] != null)
                {
                    try
                    {
                        bool selection_changed = false;

                        // don't change the order of this loop, changing and removing ops must come before adding ops
                        foreach (var op in new[] {MarketRuleEditIntent.OperationType.CHANGE_SELECTIONS, 
                                                  MarketRuleEditIntent.OperationType.REMOVE_SELECTIONS,
                                                  MarketRuleEditIntent.OperationType.ADD_SELECTIONS,
                                                  MarketRuleEditIntent.OperationType.CHANGE_DATA})
                        {
                            foreach (var edit_intent in toedit[mkt].Where(x => x.Key.Operation == op))
                            {
                                if (op == MarketRuleEditIntent.OperationType.CHANGE_SELECTIONS)
                                    selection_changed = true;

                                // we can't remove selections if an edit action changed them
                                if (op == MarketRuleEditIntent.OperationType.REMOVE_SELECTIONS && selection_changed)
                                    continue;
                                
                                _logger.DebugFormat("Performing edit (op={0}) action={1} on {2} of {3} as requested by market rules",
                                    op, edit_intent.Value, mkt, fixture);

                                edit_intent.Key.Action(mkt);
                            }
                        }

                        _logger.DebugFormat("Successfully applied edit actions on {0} of {1} as requested by market rules", mkt, fixture);

                        // as we might have changed import details of the market, we need to update the market state
                        ((IUpdatableMarketState)_currentTransaction[mkt.Id]).Update(mkt, false);

                        _logger.DebugFormat("Updating market state for {0} of {1}", mkt, fixture);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(string.Format("An error occured while executing an edit action as requested by market rules on {0} of {1}", mkt, fixture), e);

                        throw;
                    }
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

    }
}

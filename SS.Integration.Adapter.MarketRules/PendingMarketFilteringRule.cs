﻿//Copyright 2014 Spin Services Limited

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
using log4net;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules
{
    /// <summary>
    /// 
    ///     This rule removes from a snapshot/update all
    ///     the markets that are not (and never were) active.
    /// 
    ///     Once a market becomes active for the first time,
    ///     if the update is not a snapshot, the rules will
    ///     add all the missing information to the market/selections
    ///     (i.e. tags) 
    /// 
    ///     It is possible to configure the rule by sport and
    ///     allow specific markets to be excluded from this rule.
    /// 
    /// </summary>
    public class PendingMarketFilteringRule : IMarketRule
    {

        private const string NAME = "Pending_Markets";
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(PendingMarketFilteringRule));
        private readonly HashSet<string> _includedSports;
        private readonly HashSet<string> _excludedMarketTypes;
        

        public PendingMarketFilteringRule()
        {
            _includedSports = new HashSet<string>();
            _excludedMarketTypes = new HashSet<string>();
        }

        public string Name
        {
            get { return NAME; }
        }

        /// <summary>
        ///     Allows to specify a market type that will be 
        ///     excluded from the checks performed on this market rule.
        /// 
        ///     The argument must formatted as: "sport"."type"
        /// 
        ///     For example, to exclude from Football the match_winner
        ///     market, we should use:
        /// 
        ///     ExcludeMarketType("Football.match_winner");
        /// 
        /// </summary>
        /// <param name="type"></param>
        public void ExcludeMarketType(string type)
        {
            if(string.IsNullOrEmpty(type))
                return;

            _excludedMarketTypes.Add(type.ToLower());
        }

        public void ExcludeMarketType(IEnumerable<string> marketTypes)
        {
            foreach (var type in marketTypes)
                ExcludeMarketType(type);
        }

        public void RemoveExcludedMarketType(string type)
        {
            if(string.IsNullOrEmpty(type))
                return;

            _excludedMarketTypes.Remove(type.ToLower());
        }

        /// <summary>
        ///     Add a sport for the rule to applied to
        /// </summary>
        /// <param name="sport">e.g. Football, Handball etc.</param>
        public void AddSportToRule(string sport)
        {
            if(string.IsNullOrEmpty(sport))
                return;

            _includedSports.Add(sport.ToLower());
        }

        /// <summary>
        ///     Stop the rule being applied to this sport
        /// </summary>
        /// <param name="sport">e.g. Football, Handball etc.</param>
        public void RemoveSportFromRule(string sport)
        {
            if(string.IsNullOrEmpty(sport))
                return;

            _includedSports.Remove(sport.ToLower());
        }

        public IMarketRuleResultIntent Apply(Fixture fixture, IMarketStateCollection oldState, IMarketStateCollection newState)
        {
            if (_includedSports == null)
                throw new Exception($"Apply Market Rule _includedSports=NULL {fixture}");

            if (_includedSports == null)
                throw new Exception($"Apply Market Rule newState=NULL {fixture}");

            if (_includedSports == null)
                throw new Exception($"Apply Market Rule newState.Sport=NULL {fixture}");


            var result = new MarketRuleResultIntent();

            if (_includedSports.Contains(newState.Sport.ToLower()))
            {
                
                foreach (var mkt in fixture.Markets)
                {
                    string type = string.Format("{0}.{1}", newState.Sport, mkt.Type).ToLower();

                    if (_excludedMarketTypes.Contains(type))
                    {
                        _Logger.DebugFormat("market rule={0} => {1} of {2} is excluded from rule due its type={3}",
                            Name, mkt, fixture, mkt.Type);

                        continue;
                    }

                    var oldMarketState = oldState != null ? oldState[mkt.Id] : null;
                    var newMarketState = newState[mkt.Id];

                    if (oldMarketState == null)
                    {
                        //must be a snapshot then
                        if (newMarketState.IsActive)
                        {
                            //create market
                            _Logger.DebugFormat("market rule={0} => {1} of {2} is created", Name, mkt, fixture);
                            result.MarkAsUnRemovable(mkt);
                        }
                        else
                        {
                            //dont create market
                            _Logger.DebugFormat("market rule={0} => {1} of {2} is not created", Name, mkt, fixture);
                            result.MarkAsRemovable(mkt);
                        }
                    }
                    else
                    {
                        if (oldMarketState.HasBeenActive) continue;
                        if (newMarketState.IsActive)
                        {
                            //create
                            Action<Market> editMarketAction = (m =>
                            {
                                if (newMarketState.TagsCount == 0)
                                    return;

                                foreach (var tagKey in newMarketState.TagKeys)
                                {
                                    m.AddOrUpdateTagValue(tagKey, newMarketState.GetTagValue(tagKey));
                                }

                                foreach (var sel in m.Selections)
                                {
                                    var selState = newMarketState[sel.Id];
                                    foreach (var tagKey in selState.TagKeys)
                                    {
                                        sel.AddOrUpdateTagValue(tagKey, selState.GetTagValue(tagKey));
                                    }
                                }
                            });

                            var mri = new MarketRuleEditIntent(editMarketAction, MarketRuleEditIntent.OperationType.CHANGE_DATA);
                            _Logger.DebugFormat("market rule={0} => {1} of {2} is created", Name, mkt, fixture);
                            result.EditMarket(mkt, mri);
                        }
                        else
                        {
                            //dont create market
                            _Logger.DebugFormat("market rule={0} => {1} of {2} is not created", Name, mkt, fixture);
                            result.MarkAsRemovable(mkt);
                        }
                    }
                }                
            }

            return result;
        }

    }
}

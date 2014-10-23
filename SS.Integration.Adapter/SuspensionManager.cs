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
using log4net;
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter
{
    public enum SuspensionReason
    {
        SUSPENSION,
        DISCONNECT_EVENT,
        FIXTURE_DELETED,
        FIXTURE_DISPOSING,
        FIXTURE_ERRORED
    }

    public class SuspensionManager
    {

        private readonly ILog _logger = LogManager.GetLogger(typeof(SuspensionManager));

        private Action<IMarketStateCollection> _default;
        private Action<IMarketStateCollection> _disconnected;
        private Action<IMarketStateCollection> _fixtureDeleted;
        private Action<IMarketStateCollection> _disposing;
        private Action<IMarketStateCollection> _error;
        private readonly IStateProvider _stateProvider;
        private readonly IAdapterPlugin _plugin;

        internal SuspensionManager(IStateProvider stateProvider, IAdapterPlugin plugin)
        {
            if (stateProvider == null)
                throw new ArgumentNullException("stateProvider");

            if (plugin == null)
                throw new AggregateException("plugin");

            _stateProvider = stateProvider;
            _plugin = plugin;

            BuildDefaultStrategies();

            _disposing = SuspendInPlayMarketsStrategy;
            _error = SuspendFixtureStrategy;
            _disconnected = SuspendFixtureStrategy;
            _default = SuspendFixtureStrategy;
            _fixtureDeleted = SuspendFixtureAndSetMatchStatusDeleted;
            
            // Not really a singleton
            Instance = this;
        }


        public static SuspensionManager Instance
        {
            get;
            private set;
        }

        public Action<IMarketStateCollection> DoNothingStrategy
        {
            get;
            private set;
        }

        public Action<IMarketStateCollection> SuspendFixtureStrategy
        {
            get;
            private set;
        }

        public Action<IMarketStateCollection> SuspendFixtureIfInPlayStrategy
        {
            get;
            private set;
        }

        public Action<IMarketStateCollection> SuspendAllMarketsStrategy
        {
            get;
            private set;
        }

        public Action<IMarketStateCollection> SuspendInPlayMarketsStrategy
        {
            get;
            private set;
        }

        public Action<IMarketStateCollection> SuspendAllMarketsAndSetMatchStatusDeleted
        {
            get;
            private set;
        }

        public Action<IMarketStateCollection> SuspendFixtureAndSetMatchStatusDeleted
        {
            get;
            private set;
        }

        public void RegisterAction(Action<IMarketStateCollection> action, SuspensionReason reason)
        {
            switch (reason)
            {
                case SuspensionReason.FIXTURE_DISPOSING:
                    _disposing = action;
                    break;
                case SuspensionReason.DISCONNECT_EVENT:
                    _disconnected = action;
                    break;
                case SuspensionReason.FIXTURE_DELETED:
                    _fixtureDeleted = action;
                    break;
                case SuspensionReason.FIXTURE_ERRORED:
                    _error = action;
                    break;
                case SuspensionReason.SUSPENSION:
                    _default = action;
                    break;
            }

            _logger.DebugFormat("Suspend action for reason={0} has a new custom strategy", reason);
        }

        public void Suspend(string fixtureId, SuspensionReason reason = SuspensionReason.FIXTURE_DISPOSING)
        {
            Action<IMarketStateCollection> action;
            switch (reason)
            {
                case SuspensionReason.FIXTURE_DISPOSING:
                    action = _disposing;
                    break;
                case SuspensionReason.DISCONNECT_EVENT:
                    action = _disconnected;
                    break;
                case SuspensionReason.FIXTURE_DELETED:
                    action = _fixtureDeleted;
                    break;
                case SuspensionReason.FIXTURE_ERRORED:
                    action = _error;
                    break;
                default:
                    action = _default;
                    break;
            }

            IMarketStateCollection state = _stateProvider.GetMarketsState(fixtureId);
            if (state == null)
            {
                _logger.WarnFormat("State is not present for fixtureId={0} - can't suspend with reason={1}",
                    fixtureId, reason);
                return;
            }

            _logger.InfoFormat("Performing suspension for fixtureId={0} due reason={1}", fixtureId, reason);

            try
            {
                action(state);
            }
            catch (Exception e)
            {
                _logger.Error(string.Format("An error occured while performing suspend action on fixtureId={0}", fixtureId), e);
            }
        }

        private void BuildDefaultStrategies()
        {
            DoNothingStrategy = x => { };

            SuspendFixtureStrategy = x => _plugin.Suspend(x.FixtureId);

            SuspendFixtureIfInPlayStrategy = x => { if (x.FixtureStatus == MatchStatus.InRunning) _plugin.Suspend(x.FixtureId); };

            SuspendAllMarketsStrategy = x =>
                {
                    IEnumerable<IMarketState> includedMarketStates;
                    var fixture = GetFixtureWithSuspendedMarkets(x, out includedMarketStates);

                    _logger.DebugFormat("Sending suspension command through the plugin for fixtureId={0}", x.FixtureId);

                    _plugin.ProcessStreamUpdate(fixture);

                    _logger.InfoFormat("Marking markets for fixtureId={0} as suspended", x.FixtureId);
                    ((IUpdatableMarketStateCollection)x).OnMarketsForcedSuspension(includedMarketStates);
                };

            SuspendAllMarketsAndSetMatchStatusDeleted = x =>
            {
                IEnumerable<IMarketState> includedMarketStates;
                
                var fixture = GetFixtureWithSuspendedMarkets(x, out includedMarketStates);
                fixture.MatchStatus = ((int)MatchStatus.Deleted).ToString();

                _logger.DebugFormat("Sending suspension command through the plugin for fixtureId={0} and setting its MatchStatus as deleted", x.FixtureId);
                _plugin.ProcessStreamUpdate(fixture);

                _logger.InfoFormat("Marking markets for fixtureId={0} as suspended", x.FixtureId);
                ((IUpdatableMarketStateCollection)x).OnMarketsForcedSuspension(includedMarketStates);
            };

            SuspendFixtureAndSetMatchStatusDeleted = x =>
            {
                var fixture = new Fixture
                {
                    Id = x.FixtureId,
                    MatchStatus = ((int)MatchStatus.Deleted).ToString(),
                    Sequence = x.FixtureSequence
                };

                
                _logger.DebugFormat("Sending suspension command through the plugin for fixtureId={0} and setting its MatchStatus as deleted", x.FixtureId);
                _plugin.ProcessStreamUpdate(fixture);

                _logger.InfoFormat("Marking markets for fixtureId={0} as suspended", x.FixtureId);
            };

            SuspendInPlayMarketsStrategy = x =>
                {
                    List<IMarketState> includedMarketStates = new List<IMarketState>();

                    var fixture = new Fixture
                    {
                        Id = x.FixtureId,
                        MatchStatus = ((int)x.FixtureStatus).ToString(),
                        Sequence = x.FixtureSequence
                    };

                    foreach (var mkt_id in x.Markets)
                    {

                        // we take a conservative approach here.
                        // If, for any reason, the traded_in_play
                        // is not present, we assume it is. Better
                        // to suspend more markets, than less
                        IMarketState state = x[mkt_id];
                        if (state.HasTag("traded_in_play") && 
                            string.Equals(state.GetTagValue("traded_in_play"), "false", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.DebugFormat("marketId={0} of fixtureId={1} will not be suspended as it is not traded in play", mkt_id, fixture.Id);
                            continue;
                        }

                        if (!state.HasBeenActive)
                        {
                            _logger.DebugFormat("marketId={0} of fixtureId={1} will not be suspended as it has not been active before", mkt_id, fixture.Id);
                            continue;
                        }

                        includedMarketStates.Add(state);
                        fixture.Markets.Add(CreateSuspendedMarket(x[mkt_id]));
                    }


                    _logger.DebugFormat("Sending suspension command through the plugin for in-play markets of fixtureId={0}", x.FixtureId);
                    _plugin.ProcessStreamUpdate(fixture);

                    _logger.InfoFormat("Marking markets for fixtureId={0} as suspended", x.FixtureId);
                    ((IUpdatableMarketStateCollection)x).OnMarketsForcedSuspension(includedMarketStates);
                };
        }

        

        private static Fixture GetFixtureWithSuspendedMarkets(IMarketStateCollection x, out IEnumerable<IMarketState> includedMarketStates)
        {
            includedMarketStates = new List<IMarketState>();

            var fixture = new Fixture
            {
                Id = x.FixtureId,
                MatchStatus = ((int) x.FixtureStatus).ToString(),
                Sequence = x.FixtureSequence
            };

            foreach (var mkt_id in x.Markets)
            {
                ((List<IMarketState>)includedMarketStates).Add(x[mkt_id]);
                fixture.Markets.Add(CreateSuspendedMarket(x[mkt_id]));
            }

            return fixture;
        }

        private static Market CreateSuspendedMarket(IMarketState marketState)
        {
            var market = new Market(marketState.Id) { IsSuspended = true };
            foreach (var seln in marketState.Selections)
                market.Selections.Add(new Selection { Id = seln.Id, Status = SelectionStatus.Pending, Tradable = false });

            return market;
        }
    }
}

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
using System.IO;
using System.Reflection;
using log4net;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.MarketRules;
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.Model.ProcessState;

namespace SS.Integration.Adapter
{
    public class StateManager : IStoredObjectProvider, IStateManager, IStateProvider
    {
        #region Constants

        private const string PLUGIN_STORE_PREFIX = "plugin_";

        #endregion

        #region Fields

        private static readonly ILog Logger = LogManager.GetLogger(typeof(StateManager));

        private readonly ISettings _settings;
        private readonly IObjectProvider<IUpdatableMarketStateCollection> _persistanceLayer;
        private readonly IObjectProvider<IPluginFixtureState> _pluginPersistanceLayer;
        private readonly ConcurrentDictionary<string, MarketRulesManager> _rulesManagers;
        private readonly HashSet<IMarketRule> _rules;

        #endregion

        #region Constructors

        public StateManager(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _persistanceLayer = new CachedObjectStoreWithPersistance<IUpdatableMarketStateCollection>(
                new BinaryStoreProvider<IUpdatableMarketStateCollection>(
                    Path.Combine(_settings.StateProviderPath, _settings.MarketFiltersDirectory),
                    "FilteredMarkets-{0}.bin"),
                "MarketFilters", _settings.CacheExpiryInMins * 60);

            _pluginPersistanceLayer = new CachedObjectStoreWithPersistance<IPluginFixtureState>(
                new BinaryStoreProvider<IPluginFixtureState>(
                    Path.Combine(_settings.StateProviderPath, _settings.MarketFiltersDirectory),
                    "PluginStore-{0}.bin"),
                "MarketFilters", _settings.CacheExpiryInMins * 60);

            _rulesManagers = new ConcurrentDictionary<string, MarketRulesManager>();

            _rules = new HashSet<IMarketRule>
            {
                VoidUnSettledMarket.Instance,
                DeletedMarketsRule.Instance,
                InactiveMarketsFilteringRule.Instance
            };

            if (_settings.DeltaRuleEnabled)
            {
                _rules.Add(DeltaRule.Instance);
            }

            foreach (var rule in _rules)
            {
                Logger.DebugFormat("Rule {0} correctly loaded", rule.Name);
            }
        }

        #endregion

        #region Implementation of IStoredObjectProvider

        public IUpdatableMarketStateCollection GetObject(string fixtureId)
        {
            return _persistanceLayer.GetObject(fixtureId);
        }

        public void SetObject(string fixtureId, IUpdatableMarketStateCollection state)
        {
            _persistanceLayer.SetObject(fixtureId, state);
        }

        public void Remove(string fixtureId)
        {
            _persistanceLayer.Remove(fixtureId);
            RemoveFixtureStateFile(fixtureId);
        }

        #endregion

        #region Implementation of IStateProvider

        public IMarketStateCollection GetMarketsState(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                throw new ArgumentNullException(nameof(fixtureId), "fixtureId cannot be null");

            return _rulesManagers.ContainsKey(fixtureId) ? _rulesManagers[fixtureId].CurrentState : null;
        }

        public T GetPluginFixtureState<T>(string fixtureId) where T : IPluginFixtureState
        {
            var tmp = GetPluginFixtureState(fixtureId);
            if (tmp == null)
                return default(T);

            return (T)tmp;
        }

        public IPluginFixtureState GetPluginFixtureState(string fixtureId)
        {
            return _pluginPersistanceLayer.GetObject(PLUGIN_STORE_PREFIX + fixtureId);
        }

        public void AddOrUpdatePluginFixtureState(IPluginFixtureState state)
        {
            if (state == null)
                return;

            if (string.IsNullOrEmpty(state.FixtureId))
                throw new Exception("FixtureId cannot be null");

            _pluginPersistanceLayer.SetObject(PLUGIN_STORE_PREFIX + state.FixtureId, state);
        }

        public void RemovePluginState(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                throw new Exception("FixtureId cannot be null");

            _pluginPersistanceLayer.Remove(PLUGIN_STORE_PREFIX + fixtureId);
        }

        #endregion

        #region Implementation of IStateManager

        public IEnumerable<IMarketRule> LoadedRules => _rules;

        public void AddRules(IEnumerable<IMarketRule> rules)
        {
            if (rules == null)
                return;

            foreach (var rule in rules)
            {
                Logger.Debug(_rules.Add(rule)
                    ? $"Rule {rule.Name} correctly loaded"
                    : $"Rule {rule.Name} already loaded");
            }
        }

        public void OverwriteRuleList(IEnumerable<IMarketRule> rules)
        {
            if (rules != null)
            {
                _rules.Clear();
                _rules.UnionWith(rules);

                foreach (var rule in _rules)
                {
                    Logger.DebugFormat("Rule {0} correctly loaded", rule.Name);
                }
            }
        }

        public IMarketRulesManager CreateNewMarketRuleManager(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                throw new ArgumentNullException("fixtureId", "fixtureId cannot be null or empty");

            if (_rulesManagers.ContainsKey(fixtureId))
                return _rulesManagers[fixtureId];

            var ruleManager = new MarketRulesManager(fixtureId, this, _rules);
            _rulesManagers[fixtureId] = ruleManager;

            return ruleManager;
        }

        public void ClearState(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return;

            Logger.DebugFormat("Clearing data state for fixtureId={0}", fixtureId);

            if (_rulesManagers.ContainsKey(fixtureId))
            {
                MarketRulesManager dummy;
                _rulesManagers.TryRemove(fixtureId, out dummy);
            }

            _persistanceLayer.Remove(fixtureId);
            _pluginPersistanceLayer.Remove(fixtureId);
            RemoveFixtureStateFile(fixtureId);
        }

        #endregion

        #region Private methods

        private void RemoveFixtureStateFile(string fixtureId)
        {
            var fixtureStateFilePath = GetFixtureStateFilePath(fixtureId);

            if (File.Exists(fixtureStateFilePath))
            {
                File.Delete(fixtureStateFilePath);
            }
        }

        private string GetFixtureStateFilePath(string fixtureId)
        {
            var stateProviderPath = Path.IsPathRooted(_settings.StateProviderPath)
                ? _settings.StateProviderPath
                : Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    _settings.StateProviderPath);
            return Path.Combine(stateProviderPath, $"{fixtureId}.bin");
        }

        #endregion
    }
}

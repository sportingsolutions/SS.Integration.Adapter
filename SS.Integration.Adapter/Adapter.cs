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
using System.Reflection;
using System.Threading;
using SS.Integration.Adapter.Interface;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Stats;
using SS.Integration.Common.Stats.Interface;
using SS.Integration.Common.Stats.Keys;

namespace SS.Integration.Adapter
{
    public class Adapter
    {
        #region Private members

        private readonly ILog _logger = LogManager.GetLogger(typeof(Adapter).ToString());
        private readonly IStatsHandle _stats;
        private readonly ISettings _settings;
        private readonly IStateManager _stateManager;
        private readonly IServiceFacade _udapiServiceFacade;
        private readonly IAdapterPlugin _platformConnector;
        private readonly IStreamValidation _streamValidation;
        private readonly IFixtureValidation _fixtureValidation;

        #endregion

        #region Constructors

        public Adapter(
            ISettings settings,
            IServiceFacade udapiServiceFacade,
            IAdapterPlugin platformConnector,
            IStreamValidation streamValidation,
            IFixtureValidation fixtureValidation)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _udapiServiceFacade = udapiServiceFacade ?? throw new ArgumentNullException(nameof(udapiServiceFacade));
            _platformConnector = platformConnector ?? throw new ArgumentNullException(nameof(platformConnector));
            _streamValidation = streamValidation ?? throw new ArgumentNullException(nameof(streamValidation));
            _fixtureValidation = fixtureValidation ?? throw new ArgumentNullException(nameof(fixtureValidation));

            _stateManager = new StateManager(settings, platformConnector);
            StateProviderProxy.Init((IStateProvider)_stateManager);

            if (settings.StatsEnabled)
                StatsManager.Configure();

            // we just need the initialisation
            new SuspensionManager((IStateProvider)_stateManager, _platformConnector);

            platformConnector.Initialise();
            ((StateManager)_stateManager).AddRules(platformConnector.MarketRules);

            ThreadPool.SetMinThreads(500, 500);

            _stats = StatsManager.Instance["adapter.core"].GetHandle();

            PopuplateAdapterVersionInfo();
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Starts the adapter.
        /// This method returns immediately leaving to a background worker
        /// the task of getting the data and process it.
        /// 
        /// Throws an exception if it can't initialise itself.
        /// </summary>
        public void Start()
        {
            try
            {
                LogVersions();

                _logger.Info("Adapter is connecting to the UDAPI service...");

                _udapiServiceFacade.Connect();
                if (!_udapiServiceFacade.IsConnected)
                    return;

                _logger.Debug("Adapter connected to the UDAPI - initialising...");

                AdapterActorSystem.Init(
                    _settings,
                    _udapiServiceFacade,
                    _platformConnector,
                    _stateManager,
                    _streamValidation,
                    _fixtureValidation);

                _logger.InfoFormat("Adapter started");
                _stats.SetValue(AdapterCoreKeys.ADAPTER_STARTED, "1");
            }
            catch (Exception ex)
            {
                _logger.Fatal("A fatal error has occurred and the Adapater cannot start. You can try a manual restart", ex);
                throw;
            }
        }

        /// <summary>
        /// Allows to stop the adapter.
        /// 
        /// Before returning, and if it is so configured,
        /// the adapter sends a "suspend" request to 
        /// all the currently registred fixtures.
        /// </summary>
        public void Stop()
        {
            _logger.InfoFormat("Adapter is stopping");

            try
            {
                _platformConnector?.Dispose();
                _udapiServiceFacade?.Disconnect();
                AdapterActorSystem.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error("An error occured while disposing the adapter", e);
            }

            _stats.SetValue(AdapterCoreKeys.ADAPTER_STARTED, "0");
            _logger.InfoFormat("Adapter stopped");
        }

        #endregion

        #region Private methods

        private void PopuplateAdapterVersionInfo()
        {
            var adapterVersionInfo = AdapterVersionInfo.GetAdapterVersionInfo() as AdapterVersionInfo;

            var executingAssembly = Assembly.GetExecutingAssembly();
            var version = executingAssembly.GetName().Version;
            var adapterAssemblyVersion = version.ToString();

            adapterVersionInfo.AdapterVersion = adapterAssemblyVersion;

            var sdkAssembly = Assembly.GetAssembly(typeof(ISession));
            var sdkVersion = sdkAssembly.GetName().Version;
            var sdkVersionString = sdkVersion.ToString();

            adapterVersionInfo.UdapiSDKVersion = sdkVersionString;

            if (_platformConnector != null)
            {
                var pluginAssembly = Assembly.GetAssembly(_platformConnector.GetType());
                adapterVersionInfo.PluginName = pluginAssembly.GetName().Name;
                adapterVersionInfo.PluginVersion = pluginAssembly.GetName().Version.ToString();
            }
        }

        private void LogVersions()
        {
            var adapterVersionInfo = AdapterVersionInfo.GetAdapterVersionInfo();
            _logger.InfoFormat("Sporting Solutions Adapter version={0} using Sporting Solutions SDK version={1}, with plugin={2} pluginVersion={3}", adapterVersionInfo.AdapterVersion, adapterVersionInfo.UdapiSDKVersion, adapterVersionInfo.PluginName, adapterVersionInfo.PluginVersion);
        }

        #endregion
    }
}

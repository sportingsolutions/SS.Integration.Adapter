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
using System.Reflection;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using SS.Integration.Adapter.Interface;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Stats;
using SS.Integration.Common.Stats.Interface;
using SS.Integration.Common.Stats.Keys;
using System.Diagnostics;

namespace SS.Integration.Adapter
{
    public class Adapter
    {
        private readonly IStreamListenerManager _listenersManager;

        public delegate void StreamEventHandler(object sender, string fixtureId);

        private readonly static object _sync = new object();
        private readonly ILog _logger = LogManager.GetLogger(typeof(Adapter).ToString());
        
        private readonly List<string> _sports;
        
        private readonly IStatsHandle _stats;
        private Timer _trigger;

        private FixtureManager _fixtureManager;

        public Adapter(ISettings settings, IServiceFacade udapiServiceFacade, IAdapterPlugin platformConnector, IStreamListenerManager listenersManager)
        {
            _listenersManager = listenersManager;
            
            Settings = settings;
            UDAPIService = udapiServiceFacade;
            PlatformConnector = platformConnector;

            var statemanager = new StateManager(settings,platformConnector);
            StateManager = statemanager;
            StateProviderProxy.Init(statemanager);

            listenersManager.StateManager = statemanager;

            if (settings.StatsEnabled)
                StatsManager.Configure();

            // we just need the initialisation
            new SuspensionManager(statemanager, PlatformConnector);

            platformConnector.Initialise();
            statemanager.AddRules(platformConnector.MarketRules);


            ThreadPool.SetMinThreads(500, 500);
            
            _sports = new List<string>();
            
            _stats = StatsManager.Instance["adapter.core"].GetHandle();

            PopuplateAdapterVersionInfo();
        }
        
        internal IStateManager StateManager { get; set; }

        internal static IAdapterPlugin PlatformConnector { get; private set; }

        internal ISettings Settings { get; private set; }

        internal IServiceFacade UDAPIService { get; private set; }

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

                UDAPIService.Connect();
                if (!UDAPIService.IsConnected)
                    return;
                
                _logger.Debug("Adapter connected to the UDAPI - initialising...");

                foreach (var sport in UDAPIService.GetSports())
                {
                    _sports.Add(sport.Name);
                }

                _fixtureManager = new FixtureManager(Settings.FixtureCreationConcurrency, _listenersManager, UDAPIService.GetResources);

                _trigger = new Timer(timerAutoEvent => TimerEvent(), null, 0, Settings.FixtureCheckerFrequency);

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
                if (_trigger != null)
                {
                    var wait_handler = new ManualResetEvent(false);
                    _trigger.Dispose(wait_handler);
                    wait_handler.WaitOne();
                    wait_handler.Dispose();
                    _trigger = null;
                    _fixtureManager.Dispose();
                }

                if (PlatformConnector != null)
                    PlatformConnector.Dispose();

                UDAPIService.Disconnect();
            }
            catch (Exception e)
            {
                _logger.Error("An error occured while disposing the adapter", e);
            }
            
            _stats.SetValue(AdapterCoreKeys.ADAPTER_STARTED, "0");
            _logger.InfoFormat("Adapter stopped");
        }
        
        /// <summary>
        /// This method could be accessed by multi threads if the ProcessSport takes long time
        /// </summary>
        internal void TimerEvent()
        {
            try
            {
                _logger.DebugFormat("Adapter is querying API for fixtures");
                _fixtureManager.ProcessSports(_sports);
                GetStatistics();

            }
            catch (Exception ex)
            {
                if (ex is AggregateException)
                {
                    var ae = ex as AggregateException;
                    foreach (var exception in ae.InnerExceptions)
                    {
                        _logger.Error("Error processing sports: ", exception);
                    }
                }
                else
                {
                    _logger.Error("Error processing sports: ", ex);
                }

                ReconnectAPI();
            }
        }

        private void ReconnectAPI()
        {
            var success = false;
            var attempts = 0;
            while (!success || attempts > Settings.MaxRetryAttempts)
            {
                try
                {
                    lock (_sync)
                    {
                        UDAPIService.Connect();
                        success = true;
                    }
                }
                catch (Exception ex)
                {
                    attempts++;
                    _logger.ErrorFormat("Error trying to create a new session: {0}", ex);
                }
            }
            if (!success)
            {
                _logger.Warn("Failed to create a session!");
            }
        }

        internal void AddSport(string sport)
        {
            if (!_sports.Contains(sport))
                _sports.Add(sport);
        }

        private void GetStatistics()
        {
            var queueSize = _fixtureManager.QueueSize;
            var currentlyConnected = _listenersManager.ListenersCount;

            try
            {
                _stats.AddValueUnsafe(AdapterCoreKeys.ADAPTER_TOTAL_MEMORY, GC.GetTotalMemory(false).ToString());
                _stats.AddValueUnsafe(AdapterCoreKeys.ADAPTER_RUNNING_THREADS, Process.GetCurrentProcess().Threads.Count.ToString());
                _stats.SetValueUnsafe(AdapterCoreKeys.ADAPTER_HEALTH_CHECK, "1");
                _stats.SetValueUnsafe(AdapterCoreKeys.ADAPTER_FIXTURE_TOTAL, currentlyConnected.ToString());

                foreach (var sport in _listenersManager.GetListenersBySport())
                {
                    _stats.SetValueUnsafe(string.Format(AdapterCoreKeys.SPORT_FIXTURE_TOTAL, sport.Key), sport.Count().ToString());
                    _stats.SetValueUnsafe(string.Format(AdapterCoreKeys.SPORT_FIXTURE_STREAMING_TOTAL, sport.Key), sport.Count(x => x.IsStreaming).ToString());
                    _stats.SetValueUnsafe(string.Format(AdapterCoreKeys.SPORT_FIXTURE_IN_PLAY_TOTAL, sport.Key), sport.Count(x => x.IsInPlay).ToString());
                }
            }
            catch { }

            _logger.DebugFormat("Currently adapter is streaming fixtureCount={0} and creation queue has queueSize={1} elements", currentlyConnected, queueSize);

        }
        
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

            if (PlatformConnector != null)
            {
                var pluginAssembly = Assembly.GetAssembly(PlatformConnector.GetType());
                adapterVersionInfo.PluginName = pluginAssembly.GetName().Name;
                adapterVersionInfo.PluginVersion = pluginAssembly.GetName().Version.ToString();
            }
        }
        
        private void LogVersions()
        {
            var adapterVersionInfo = AdapterVersionInfo.GetAdapterVersionInfo();
            _logger.InfoFormat("Sporting Solutions Adapter version={0} using Sporting Solutions SDK version={1}, with plugin={2} pluginVersion={3}", adapterVersionInfo.AdapterVersion, adapterVersionInfo.UdapiSDKVersion, adapterVersionInfo.PluginName, adapterVersionInfo.PluginVersion);
        }

    }
}

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
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Configuration;
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;
using Ninject.Modules;
using SS.Integration.Adapter.Interface;
using log4net;
using Ninject;
using SS.Integration.Adapter.Diagnostics;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.WindowsService
{
    public partial class AdapterService : ServiceBase
    {
        #region Private members

        private readonly ILog _logger = LogManager.GetLogger(typeof(AdapterService).ToString());
        private static Task _adapterWorkerThread;
        private Adapter _adapter;
        private int _fatalExceptionsCounter = 0;
        private bool _skipRestartOnFatalException;

        #endregion

        #region Properties

        [Import]
        public IAdapterPlugin PlatformConnector { get; set; }

        [Import(AllowDefault = true)]
        public IPluginBootstrapper<NinjectModule> PluginBootstrapper { get; set; }

        #endregion

        #region Constructors

        public AdapterService()
        {
            InitializeComponent();
            
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

            Compose();
        }

        #endregion

        #region Protected methods

        protected override void OnStart(string[] args)
        {
            _adapterWorkerThread = Task.Factory.StartNew(InitialiseAdapter, TaskCreationOptions.LongRunning);
            _adapterWorkerThread.ContinueWith(t =>
            {
                if (t.IsFaulted || t.Status == TaskStatus.Faulted)
                {
                    _logger.FatalFormat("Problem starting adapter {0}", t.Exception);
                }
            });
        }

        protected override void OnStop()
        {
            _logger.Info("Requesting Adapter Stop");

            _adapter.Stop();
            _adapterWorkerThread.Wait();
            _adapterWorkerThread.ContinueWith(task => { _logger.InfoFormat("Adapter successfully stopped"); Environment.Exit(0); });
        }

        #endregion

        #region Private methods

        private void InitialiseAdapter()
        {
            _logger.Info("Requesting Adapter Start");

            if (PlatformConnector == null)
            {
                _logger.Fatal("Plugin could not be found. Ensure that plugin is copied in folder and restart the service");
                return;
            }

            List<INinjectModule> modules = new List<INinjectModule> { new BootStrapper() };

            if (PluginBootstrapper != null)
            {
                _logger.InfoFormat("Plugin Bootstrapper found of type={0}", PluginBootstrapper.GetType().Name);
                modules.AddRange(PluginBootstrapper.BootstrapModules);
            }

            StandardKernel iocContainer = new StandardKernel(modules.ToArray());


            var settings = iocContainer.Get<ISettings>();
            var service = iocContainer.Get<IServiceFacade>();
            var streamValidation = iocContainer.Get<IStreamValidation>();
            var fixtureValidation = iocContainer.Get<IFixtureValidation>();
            var objectProvider = iocContainer.Get<IObjectProvider<Dictionary<string, FixtureOverview>>>();

             iocContainer.Settings.InjectNonPublic = true;

            //needed for Plugin properties since plugin is not instantiated by Ninject
            iocContainer.Inject(PlatformConnector);

            _adapter =
                new Adapter(
                    settings,
                    service,
                    PlatformConnector,
                    streamValidation,
                    fixtureValidation);

            _adapter.Start();

            if (settings.UseSupervisor)
            {
                SupervisorStartUp.Initialize(objectProvider);
            }

            _logger.Info("Adapter has started");
        }

        private void RestartAdapter()
        {
            if (_skipRestartOnFatalException)
                return;

            _fatalExceptionsCounter++;
            _adapter.Stop();
            int maxFailures = GetMaxFailures();

            //0 means no limit
            maxFailures = maxFailures > 0 ? maxFailures : int.MaxValue;

            if (maxFailures > _fatalExceptionsCounter)
            {
                _adapter.Start();
            }
            else
            {
                _logger.WarnFormat("Adapter registered {0} FATAL/Unhandled exceptions and will stop the service now", GetMaxFailures());
            }
        }

        private int GetMaxFailures()
        {
            int maxFailures = 0;
            int.TryParse(ConfigurationManager.AppSettings["maxUnhandledExceptions"], out maxFailures);
            _skipRestartOnFatalException = !bool.Parse(ConfigurationManager.AppSettings["skipRestartOnFatalException"]) ;

            return maxFailures;
        }

        private void Compose()
        {
            _logger.Info("Adapter Service is looking for a plugin");
            CompositionContainer container = null;

            try
            {
                string codebase = AppDomain.CurrentDomain.BaseDirectory;

                var pluginAssembly = ConfigurationManager.AppSettings["pluginAssembly"];
                var catalog = new SafeDirectoryCatalog(codebase, pluginAssembly);
                container = new CompositionContainer(catalog);
                container.ComposeParts(this);
            }
            catch (CompositionException ex)
            {
                foreach (var error in ex.Errors)
                {
                    _logger.Fatal("Error when loading plugin", error.Exception);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var error in ex.LoaderExceptions)
                {
                    _logger.Fatal("Error when searching for plugin", error);
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal("Error when loading plugin", ex);
            }
            finally
            {
                if (container != null)
                {
                    container.Dispose();
                }
            }
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger.FatalFormat("Adapter termination in progress={1} caused by UNHANDLED Exception {0}", (Exception)e.ExceptionObject, e.IsTerminating);
            
            if(e.IsTerminating)
                OnStop();
            else
                RestartAdapter();
        }

        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArgs)
        {
            unobservedTaskExceptionEventArgs.SetObserved();
            if (unobservedTaskExceptionEventArgs.Exception is AggregateException)
            {
                foreach (var exception in unobservedTaskExceptionEventArgs.Exception.Flatten().InnerExceptions)
                {
                    _logger.Fatal("Adapter received unobserved exception from TaskScheduler: ", exception);
                }

            }
            else
            {
                _logger.Fatal("Adapter received unobserved exception from TaskScheduler: ", unobservedTaskExceptionEventArgs.Exception);
            }

            RestartAdapter();
        }

        #endregion
    }
}

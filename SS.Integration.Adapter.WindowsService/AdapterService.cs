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
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;
using SS.Integration.Adapter.Interface;
using log4net;
using Ninject;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.ProcessState;

namespace SS.Integration.Adapter.WindowsService
{
    public partial class AdapterService : ServiceBase
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(AdapterService).ToString());
        private static Task _adapterWorkerThread; 

        private Adapter _adapter;

        private readonly StandardKernel _iocContainer;

        [Import]
        public IAdapterPlugin PlatformConnector { get; set; }

        public AdapterService()
        {
            InitializeComponent();

            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;    

            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;

            _iocContainer = new StandardKernel(new BootStrapper());

            Compose();
        }

        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs unobservedTaskExceptionEventArgs)
        {
            unobservedTaskExceptionEventArgs.SetObserved();
            if (unobservedTaskExceptionEventArgs.Exception is AggregateException)
            {
                foreach (var exception in unobservedTaskExceptionEventArgs.Exception.Flatten().InnerExceptions)
                {
                    _logger.Fatal("Adapter received unobserved exception from TaskScheduler: ",exception);
                }
                
            }
            else
            {
                _logger.Fatal("Adapter received unobserved exception from TaskScheduler: ", unobservedTaskExceptionEventArgs.Exception);
            }
            
            
        }

        private void Compose()
        {
            CompositionContainer container = null;

            try
            {
                string codebase = AppDomain.CurrentDomain.BaseDirectory;

                var catalog = new DirectoryCatalog(codebase);
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

        internal void InitialiseAdapter()
        {
            _logger.Info("Requesting GTPService Adapter Start");

            if (PlatformConnector == null)
            {
                _logger.Fatal("Plugin could not be found. Ensure that plugin is copied in folder and restart the service");
                return;
            }

            var connector = PlatformConnector;
            connector.Initialise();

            var settings = _iocContainer.Get<ISettings>();
            var service = _iocContainer.Get<IServiceFacade>();

            var listenerFactoryMethod =
                _iocContainer.Get<Func<string, IResourceFacade, Fixture, IAdapterPlugin, IEventState,IObjectProvider<IMarketStateCollection>,  int, IListener>>();
            var eventState = EventState.Create(new FileStoreProvider(), settings);

            _adapter = new Adapter(settings, service, connector, eventState, listenerFactoryMethod);

            _adapter.Start();

            _logger.Info("Adapter has started.");
        }

        protected override void OnStop()
        {
            _logger.Info("Requesting GTPService Adapter Stop");

            if (PlatformConnector != null)
            {
                _logger.Info("Stopping Plugin's HeartBeat");

                PlatformConnector.Dispose();
            }

            _adapter.Stop();
            _adapterWorkerThread.Wait();
            _adapterWorkerThread.ContinueWith(task => _logger.InfoFormat("Adapter successfully stopped"));
        }

        void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger.Fatal("Unhandled Exception, stopping service", (Exception)e.ExceptionObject);

            _adapter.Stop();
            _adapter.Start();
        }
    }
}

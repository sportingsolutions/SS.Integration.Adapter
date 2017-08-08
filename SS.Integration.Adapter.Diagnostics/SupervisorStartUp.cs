using System;
using System.Collections.Generic;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Diagnostics.Actors;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Diagnostics.RestService;
using SS.Integration.Adapter.Diagnostics.RestService.PushNotifications;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Diagnostics
{
    public static class SupervisorStartUp
    {
        #region Fields

        private static readonly ILog Logger = LogManager.GetLogger(typeof(SupervisorStartUp).ToString());
        private static ISupervisorService _service;
        private static ISupervisorProxy _proxy;
        private static IActorRef _supervisorActor;

        #endregion

        #region Public methods

        public static void Initialize(IObjectProvider<Dictionary<string, FixtureOverview>> objectProvider)
        {
            // SS.Integration.Diagnostics.RestService uses Owin.HttpListeners.
            // that assembly must be referenced in the startup project even if not
            // directly used, so do not remove it from the list of references
            Logger.Info("Initializing adapter's supervisor.");
            try
            {
                //initialize default service configuration
                var serviceConfiguration = new ServiceConfiguration();

                //initialize streaming service
                var streamingService = serviceConfiguration.UsePushNotifications
                    ? (ISupervisorStreamingService)SupervisorStreamingService.Instance
                    : SupervisorNullStreamingService.Instance;

                //initialize supervisor actor
                _supervisorActor = AdapterActorSystem.ActorSystem.ActorOf(
                    Props.Create(() => new SupervisorActor(streamingService, objectProvider)),
                    SupervisorActor.ActorName);

                //initialize supervisor proxy
                _proxy = new SupervisorProxy(_supervisorActor);

                //initialize and start supervisor service
                _service = new Service(serviceConfiguration, _proxy);
                _service.Start();
            }
            catch (Exception e)
            {
                string errMsg =
                    "An error occured during the initialization of the adapter's supervisor.";
                Logger.Error(errMsg, e);
            }
        }

        public static void Dispose()
        {
            AdapterActorSystem.ActorSystem?.Stop(_supervisorActor);
            _service.Stop();
            _proxy.Dispose();
        }

        #endregion
    }
}

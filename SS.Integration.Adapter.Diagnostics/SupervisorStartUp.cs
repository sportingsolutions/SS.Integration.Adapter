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
        #region Private members

        private static readonly ILog Logger = LogManager.GetLogger(typeof(SupervisorStartUp).ToString());

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
                var actorRef = AdapterActorSystem.ActorSystem.ActorOf(
                    Props.Create(() => new SupervisorActor(streamingService, objectProvider)),
                    SupervisorActor.ActorName);

                AdapterActorSystem.SupervisorActor = actorRef;

                //initialize supervisor proxy
                var proxy = new SupervisorProxy(actorRef);

                //initialize and start supervisor service
                var service = new Service(serviceConfiguration, proxy);
                service.Start();
            }
            catch (Exception e)
            {
                string errMsg =
                    "An error occured during the initialization of the adapter's supervisor.";
                Logger.Error(errMsg, e);
            }
        }

        #endregion
    }
}

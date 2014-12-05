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
using System.IO;
using System.Web.Http;
using log4net;
using Microsoft.Owin.Cors;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.StaticFiles;
using Owin;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Diagnostics.RestService.PushNotifications;

namespace SS.Integration.Adapter.Diagnostics.RestService
{
    public class Service : ISupervisorService
    {
        // do not change this to use HttpSelfHostServer to self hosting asp.net web api 
        // cause SignalR requires an OWIN host
        // make sure Microsoft.Owin.Host.HttpListner is reference otherwise a run-time exception is raised.

        private IDisposable _server;
        private readonly ILog _log = LogManager.GetLogger(typeof(Service));

        public Service(ISupervisorProxy supervisor)
            : this(supervisor, GetDefaultConfiguration())
        {
        }

        public Service(ISupervisorProxy supervisor, ISupervisorServiceConfiguration configuration)
        {
            if (supervisor == null)
                throw new ArgumentNullException("supervisor");

            if (configuration == null)
                throw new ArgumentNullException("configuration");
            
            Supervisor = supervisor;
            ServiceConfiguration = configuration;
            ServiceInstance = this;
        }

        #region ISupervisorService

        public ISupervisorProxy Supervisor { get; private set; }

        public ISupervisorServiceConfiguration ServiceConfiguration { get; private set; }

        public ISupervisorStreamingService StreamingService
        {
            get
            {
                if(ServiceConfiguration.UsePushNotifications)
                    return SupervisorStreamingService.Instance;

                return SupervisorNullStreamingService.Instance;
            }
        }

        public void Start()
        {
            _log.InfoFormat("Starting self-hosted web server on {0}", ServiceConfiguration.Url);
            _server = WebApp.Start<ServiceStartUp>(ServiceConfiguration.Url);
        }

        public void Stop()
        {
            _log.Info("Stopping self-hosted web server");

            if (_server != null)
                _server.Dispose();

            _log.Info("Self-hosted web server stopped");
        }

        #endregion

        internal static ISupervisorServiceConfiguration GetDefaultConfiguration()
        {
            return new ServiceConfiguration();
        }

        /// <summary>
        /// Global static access to this object instance
        /// </summary>
        public static ISupervisorService ServiceInstance { get; private set; }

        /// <summary>
        /// Start up class required by OWIN
        /// ServiceStartUp.Configuration(IAppBuilder app) is calle directly by the OWIN runtime
        /// </summary>
        private class ServiceStartUp
        {

            public void Configuration(IAppBuilder app)
            {
                // configure the OWIN middlewares....pay attention that order matters

                if (ServiceInstance.ServiceConfiguration.UsePushNotifications)
                    UseSignalR(app);

                UseFileServer(app);
                UseWebApi(app);
            }

            private static void UseSignalR(IAppBuilder app)
            {
                // prepare the signalr middleware
                app.UseCors(CorsOptions.AllowAll);
                app.MapSignalR("/streaming", new Microsoft.AspNet.SignalR.HubConfiguration
                {
                    EnableJavaScriptProxies = false,
                    EnableJSONP = false,
                    EnableDetailedErrors = false
                });
            }

            private static void UseWebApi(IAppBuilder app)
            {
                // prepare the web-api middleware
                HttpConfiguration config = new HttpConfiguration();

                config.MapHttpAttributeRoutes();

                config.Routes.MapHttpRoute(
                    name: "Error404",
                    routeTemplate: "{*url}", // invalid URL character, so this will never be called externally
                    defaults: new { controller = "Error", action = "Handle404" }
                );


                app.UseWebApi(config);
            }

            private static void UseFileServer(IAppBuilder app)
            {
                // configure the static web server middleware
                var options = new FileServerOptions
                {
                    FileSystem = new PhysicalFileSystem(GetRootDirectory(ServiceInstance.ServiceConfiguration.UIPath)),
                    EnableDirectoryBrowsing = true,
                    RequestPath = new Microsoft.Owin.PathString("/ui"),
                    EnableDefaultFiles = true
                };

                app.UseFileServer(options);
            }

            private static string GetRootDirectory(string path)
            {
                var currentDirectory = Directory.GetCurrentDirectory();

                if (path.StartsWith("/"))
                {
                    path = path.Substring(1);
                }

                return Path.Combine(currentDirectory, path);
            }
        }
    }
}
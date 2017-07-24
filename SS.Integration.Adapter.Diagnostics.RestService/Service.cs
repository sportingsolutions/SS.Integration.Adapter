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
using System.Reflection;
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
    // do not change this to use HttpSelfHostServer to self hosting asp.net web api 
    // cause SignalR requires an OWIN host
    // make sure Microsoft.Owin.Host.HttpListner is reference otherwise a run-time exception is raised.
    public sealed class Service : ISupervisorService
    {
        #region Private members

        private IDisposable _server;
        private readonly ILog _log = LogManager.GetLogger(typeof(Service));

        #endregion

        #region Properties

        /// <summary>
        /// Global static access to this object instance
        /// </summary>
        internal static ISupervisorService Instance { get; private set; }

        #endregion

        #region Constructors

        public Service(ISupervisorServiceConfiguration configuration, ISupervisorProxy proxy)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));

            Instance = this;
        }

        #endregion

        #region Implementation of ISupervisorService

        public ISupervisorProxy Proxy { get; private set; }

        public ISupervisorServiceConfiguration Configuration { get; private set; }

        public void Start()
        {
            _log.InfoFormat("Starting self-hosted web server on {0}", Configuration.Url);
            _server = WebApp.Start<ServiceStartUp>(Configuration.Url);
        }

        public void Stop()
        {
            _log.Info("Stopping self-hosted web server");

            _server?.Dispose();

            _log.Info("Self-hosted web server stopped");
        }

        #endregion

        #region Private Sub Types

        /// <summary>
        /// Start up class required by OWIN
        /// ServiceStartUp.Configuration(IAppBuilder app) is called directly by the OWIN runtime
        /// </summary>
        private class ServiceStartUp
        {

            public void Configuration(IAppBuilder app)
            {
                // configure the OWIN middlewares....pay attention that order matters

                if (Instance.Configuration.UsePushNotifications)
                    UseSignalR(app);

                UseFileServer(app);
                UseWebApi(app);
            }

            private static void UseSignalR(IAppBuilder app)
            {
                // prepare the signalr middleware
                app.UseCors(CorsOptions.AllowAll);
                app.MapSignalR(Instance.Configuration.PushNotificationsPath, new Microsoft.AspNet.SignalR.HubConfiguration
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
                    FileSystem = new PhysicalFileSystem(GetRootDirectory(Instance.Configuration.UIPath)),
                    EnableDirectoryBrowsing = true,
                    RequestPath = new Microsoft.Owin.PathString(Instance.Configuration.UIPath),
                    EnableDefaultFiles = true
                };

                app.UseFileServer(options);
            }

            private static string GetRootDirectory(string path)
            {
                var currentDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

                if (path.StartsWith("/"))
                {
                    path = path.Substring(1);
                }

                return Path.Combine(currentDirectory, path);
            }
        }

        #endregion
    }
}
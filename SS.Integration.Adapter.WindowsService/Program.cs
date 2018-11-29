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
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using log4net;

namespace SS.Integration.Adapter.WindowsService
{
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static void Main()
        {
	        Console.WriteLine("Attach debbuger and press enter to start");
			Console.ReadLine();
	        SetCulture();
            var path = Assembly.GetExecutingAssembly().Location;
            var fileInfo = new FileInfo(path);
            var dir = fileInfo.DirectoryName;
            log4net.Config.XmlConfigurator.ConfigureAndWatch(new FileInfo(string.Format("{0}\\log4net.config", dir)));

            var logger = LogManager.GetLogger(typeof(Program).ToString());

            logger.Info("Initialising SportingSolutions Integration WindowsService");

            var servicesToRun = new ServiceBase[]
                {
                    new AdapterService()
                };
            logger.Info("App culture=" + CultureInfo.DefaultThreadCurrentCulture);

            if (Environment.UserInteractive)
            {
                var type = typeof(ServiceBase);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var method = type.GetMethod("OnStart", flags);
                var onstop = type.GetMethod("OnStop", flags);

                foreach (var service in servicesToRun)
                {
                    method.Invoke(service, new object[] { null });
                }

                logger.Info(@"Service Started! - Press any key to stop");
                Console.ReadLine();

                if (onstop != null)
                {
                    foreach (var service in servicesToRun)
                    {
                        onstop.Invoke(service, null);
                    }
                }
            }
            else
            {
                logger.Info("Attempting to run Adapter Service");
                ServiceBase.Run(servicesToRun);
            }
        }

        static void SetCulture()
        {
            var cultureName = ConfigurationManager.AppSettings["enforceCulture"];
            if (string.IsNullOrWhiteSpace(cultureName))
                cultureName = "en-GB";
            cultureName = cultureName.Trim();
            CultureInfo culture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
        }
    }
}

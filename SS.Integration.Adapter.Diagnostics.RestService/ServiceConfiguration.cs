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

using System.Configuration;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;

namespace SS.Integration.Adapter.Diagnostics.RestService
{
    public class ServiceConfiguration : ISupervisorServiceConfiguration
    {
        private const string DEFAULT_URL = "http://localhost:9000";
        private const string DEFAULT_PUSH_PATH = "/streaming";
        private const string DEFAULT_UI_PATH = "/ui";
        private const bool DEFAULT_USE_PUSH = true;
        private const bool DEFAULT_USE_UI_REDIRECT = true;

        public ServiceConfiguration()
        {
            var configuredUrl = ConfigurationManager.AppSettings["SupervisorUrl"];
            Url = string.IsNullOrEmpty(configuredUrl) ? DEFAULT_URL : configuredUrl;
            UsePushNotifications = DEFAULT_USE_PUSH;
            PushNotificationsPath = DEFAULT_PUSH_PATH;
            UIPath = DEFAULT_UI_PATH;
            UseUIRedirect = DEFAULT_USE_UI_REDIRECT;
        }

        public string Url { get; set; }

        public bool UsePushNotifications { get; set; }
        
        public string PushNotificationsPath { get; set; }
        
        public string UIPath { get; set; }
        
        public bool UseUIRedirect { get; set; }
    }
}

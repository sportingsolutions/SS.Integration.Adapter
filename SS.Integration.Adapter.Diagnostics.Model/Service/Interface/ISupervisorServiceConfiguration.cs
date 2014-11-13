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


namespace SS.Integration.Adapter.Diagnostics.Model.Service.Interface
{
    public interface ISupervisorServiceConfiguration
    {
        /// <summary>
        /// Fully qualified URL the service will listen to.
        /// I.e: http://localhost:9000
        /// </summary>
        string Url { get; }

        /// <summary>
        /// True if the service will generate push
        /// notifications to this address
        /// Url + PushNotificationsPath
        /// </summary>
        bool UsePushNotifications { get; }

        /// <summary>
        /// Url path where the push notifications service
        /// is listening.
        /// </summary>
        string PushNotificationsPath { get; }

        /// <summary>
        /// Url path where the UI lies
        /// </summary>
        string UIPath { get; }

        /// <summary>
        /// If true, every request not found 
        /// whose path starts with UIPath
        /// will be redirected to UIPath/index.html
        /// </summary>
        bool UseUIRedirect { get; }
    }
}

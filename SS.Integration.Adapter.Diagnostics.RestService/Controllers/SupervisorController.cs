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

using System.Net.Http;
using System.Web.Http;
using System.Net;
using SS.Integration.Adapter.Diagnostics.RestService.Attributes;
using SS.Integration.Adapter.Diagnostics.RestService.Models;

namespace SS.Integration.Adapter.Diagnostics.RestService.Controllers
{
    [HandleServerError]
    [RoutePrefix("api/supervisor")]
    public class SupervisorController : ApiController
    {
        [Route("details")]
        [HttpGet]
        public HttpResponseMessage GetDetails()
        {
            AdapterStatus status = new AdapterStatus
            {
                AdapterVersion = "1.2.3",
                IsRunning = true,
                PluginName = "Testing",
                PluginVersion = "4.5.6",
                UdapiSDKVersion = "7.8.9",
                RunningThreads = "12",
                MemoryUsage = "150000"
            };            

            return Request.CreateResponse(HttpStatusCode.OK, status, UrlUtilities.JSON_MEDIA_TYPE);
        }
    }
}
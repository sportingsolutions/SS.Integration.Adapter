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
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;

namespace SS.Integration.Adapter.Diagnostic.RestService.Handlers
{
    public class HttpNotFoundControllerSelector : DefaultHttpControllerSelector
    {
        public HttpNotFoundControllerSelector(HttpConfiguration configuration) 
            : base(configuration)
        {
        }

        public override HttpControllerDescriptor SelectController(HttpRequestMessage request)
        {
            HttpControllerDescriptor descriptor = null;

            try
            {
                descriptor = base.SelectController(request);
            }
            catch (HttpResponseException ex)
            {
                var code = ex.Response.StatusCode;
                if (code != System.Net.HttpStatusCode.NotFound)
                    throw;

                var routeValues = request.GetRouteData().Values;
                routeValues["controller"] = "Error";
                routeValues["action"] = "Handle404";
                descriptor = base.SelectController(request);

            }

            return descriptor;
        }
    }
}
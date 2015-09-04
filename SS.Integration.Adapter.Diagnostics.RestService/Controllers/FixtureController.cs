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

using System.Net;
using System.Net.Http;
using System.Web.Http;
using SS.Integration.Adapter.Diagnostics.RestService.Attributes;

namespace SS.Integration.Adapter.Diagnostics.RestService.Controllers
{
    [HandleServerError]
    [RoutePrefix("api/supervisor/fixture")]
    public class FixtureController : ApiController
    {
        [Route("{fixtureId}")]
        [Route("{fixtureId}/details")]
        [HttpGet]
        public HttpResponseMessage GetDetails(string fixtureId)
        {
            var details = Service.ServiceInstance.Supervisor.GetFixtureDetail(fixtureId);
            if(details == null)
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            var res = Request.CreateResponse(HttpStatusCode.OK, details, UrlUtilities.JSON_MEDIA_TYPE);
            res.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            return res;
        }

        [Route("{fixtureId}/history")]
        [HttpGet]
        public HttpResponseMessage GetHistory(string fixtureId)
        {
            var history = Service.ServiceInstance.Supervisor.GetFixtureHistory(fixtureId);
            if (history == null)
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            var res = Request.CreateResponse(HttpStatusCode.OK, history, UrlUtilities.JSON_MEDIA_TYPE);
            res.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
            return res;
        }

        [Route("{fixtureId}/takesnapshot")]
        [HttpPost]
        public HttpResponseMessage TakeSnapshot(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            if(Service.ServiceInstance.Supervisor.TakeSnapshot(fixtureId))
                return new HttpResponseMessage(HttpStatusCode.OK);

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        [Route("{fixtureId}/restartlistener")]
        [HttpPost]
        public HttpResponseMessage RestartListener(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            if(Service.ServiceInstance.Supervisor.RestartListener(fixtureId))
                return new HttpResponseMessage(HttpStatusCode.OK);

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        [Route("{fixtureId}/clearstate")]
        [HttpPost]
        public HttpResponseMessage ClearState(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            if (Service.ServiceInstance.Supervisor.ClearState(fixtureId))
                return new HttpResponseMessage(HttpStatusCode.OK);

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }
}
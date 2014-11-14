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

namespace SS.Integration.Adapter.Diagnostics.RestService.Controllers
{
    [HandleServerError]
    [RoutePrefix("api/supervisor")]
    [Route("{action}")]
    public class SportController : ApiController
    {

        [Route("~/")]      // GET /
        [Route]            // GET api/supervisor/
        [Route("sports")]  // GET api/supervisor/sports
        [HttpGet]
        public HttpResponseMessage GetSports()
        {
            var sports = Service.ServiceInstance.Supervisor.GetSports();
            return Request.CreateResponse(HttpStatusCode.OK, sports, UrlUtilities.JSON_MEDIA_TYPE);
        }

        [Route("sports/{sportCode}")]
        [HttpGet]
        public HttpResponseMessage GetSport(string sportCode)
        {
            var sport = Service.ServiceInstance.Supervisor.GetSportDetail(sportCode);
            if(sport == null)
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            return Request.CreateResponse(HttpStatusCode.OK, sport, UrlUtilities.JSON_MEDIA_TYPE);
        }
    }
}
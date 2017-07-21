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
    [RoutePrefix("api/supervisor/search")]
    public class SearchController : ApiController
    {
        [Route("fixture")]
        [HttpPost]
        public HttpResponseMessage SearchFixture([FromBody] string fixtureId)
        {
            if (!string.IsNullOrEmpty(fixtureId))
            {

                foreach (var fixture in Service.Instance.Proxy.GetFixtures())
                {
                    if (string.Equals(fixtureId, fixture.Id))
                    {
                        var res = Request.CreateResponse(HttpStatusCode.OK, fixture, UrlUtilities.JSON_MEDIA_TYPE);
                        res.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                    }
                }
            }

            // just send 200 with no result if fixture is not found
            return Request.CreateResponse(HttpStatusCode.OK);
        }
    }
}

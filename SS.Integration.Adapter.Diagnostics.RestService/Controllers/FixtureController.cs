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
using SS.Integration.Adapter.Diagnostic.RestService.Attributes;
using SS.Integration.Adapter.Diagnostic.RestService.Models;

namespace SS.Integration.Adapter.Diagnostic.RestService.Controllers
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
            var tmp = new FixtureDetail
            {
                Id = "asdas341",
                IsStreaming = true,
                State = FixtureOverview.FixtureState.Ready,
                Competition = "French Division 1",
                CompetitionId = "1qqqqqq",
                StartTime = new System.DateTime(2014, 3, 17, 17, 0, 0),
                Description = "PSG v Lion",
                Sequence = "5",
                IsIgnored = false,
                IsDeleted = false,
                ConnectionState = FixtureDetail.ConnectionStatus.CONNECTED
            };

            tmp.ProcessingEntries.Add(new FixtureProcessingEntry { Sequence = "1", Epoch = "1", IsUpdate = false, State = FixtureProcessingEntry.FixtureProcessingState.PROCESSED, Timestamp = new System.DateTime(2013, 06, 11, 14, 33, 0)});
            tmp.ProcessingEntries.Add(new FixtureProcessingEntry { Sequence = "2", Epoch = "1", IsUpdate = true, Exception = "Null pointer exception", State = FixtureProcessingEntry.FixtureProcessingState.PROCESSED, Timestamp = new System.DateTime(2013, 06, 11, 14, 34, 0) });
            tmp.ProcessingEntries.Add(new FixtureProcessingEntry { Sequence = "2", Epoch = "1", IsUpdate = false, State = FixtureProcessingEntry.FixtureProcessingState.PROCESSED, Timestamp = new System.DateTime(2013, 06, 11, 14, 34, 30) });
            tmp.ProcessingEntries.Add(new FixtureProcessingEntry { Sequence = "3", IsUpdate = true, State = FixtureProcessingEntry.FixtureProcessingState.SKIPPED, Timestamp = new System.DateTime(2013, 06, 11, 14, 35, 0) });
            tmp.ProcessingEntries.Add(new FixtureProcessingEntry { Sequence = "4", IsUpdate = true, State = FixtureProcessingEntry.FixtureProcessingState.PROCESSED, Timestamp = new System.DateTime(2013, 06, 11, 14, 37, 0) });
            tmp.ProcessingEntries.Add(new FixtureProcessingEntry { Sequence = "5", Epoch = "2", IsUpdate = true, EpochChangeReason = "10", State = FixtureProcessingEntry.FixtureProcessingState.SKIPPED, Timestamp = new System.DateTime(2013, 06, 11, 14, 38, 45) });
            tmp.ProcessingEntries.Add(new FixtureProcessingEntry { Sequence = "5", Epoch = "2", IsUpdate = false, State = FixtureProcessingEntry.FixtureProcessingState.PROCESSING, Timestamp = new System.DateTime(2013, 06, 11, 14, 39, 0) });

            return Request.CreateResponse(HttpStatusCode.OK, tmp, UrlUtilities.JSON_MEDIA_TYPE);

            if (string.IsNullOrEmpty(fixtureId))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [Route("{fixtureId}/history")]
        [HttpGet]
        public HttpResponseMessage GetHistory(string fixtureId)
        {
            if (string.IsNullOrEmpty(fixtureId))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
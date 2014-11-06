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
using SS.Integration.Adapter.Diagnostic.RestService.Attributes;
using SS.Integration.Adapter.Diagnostic.RestService.Models;

namespace SS.Integration.Adapter.Diagnostic.RestService.Controllers
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
            
            System.Collections.Generic.List<SportDetail> sports = new System.Collections.Generic.List<SportDetail>();
            foreach(var sport in new[] {"Football", "RugbyUnion", "RugbyLeague", "Darts", "Cricket", "TestCricket", "AmericanFootball", "Basketball", "Baseball", "HorseRacing"})
            {
                sports.Add(GenerateMockedSportDetail(sport));
            }
        
            return Request.CreateResponse(HttpStatusCode.OK, sports, UrlUtilities.JSON_MEDIA_TYPE);
        }

        [Route("sports/{sportCode}")]
        [HttpGet]
        public HttpResponseMessage GetSport(string sportCode)
        {
            // TODO call the supervisor for getting these data


            return Request.CreateResponse(HttpStatusCode.OK, GenerateMockedSportDetail(sportCode), UrlUtilities.JSON_MEDIA_TYPE);

            if (string.IsNullOrEmpty(sportCode))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private SportDetail GenerateMockedSportDetail(string sportCode)
        {
            SportDetail detail = new SportDetail(sportCode);
            detail.Fixtures.Add(new FixtureOverview { Id = "1", IsStreaming = true, State = FixtureOverview.FixtureState.Running, Competition = "Premier League", CompetitionId = "123212112", StartTime = new System.DateTime(2014, 2, 17, 9, 0, 0), Description = "Chelsea v QPR", Sequence = "10" });
            detail.Fixtures.Add(new FixtureOverview { Id = "2", IsStreaming = true, State = FixtureOverview.FixtureState.PreMatch, IsInErrorState = true, Competition = "Premier League", CompetitionId = "ffffff", StartTime = new System.DateTime(2014, 2, 17, 14, 0, 0), Description = "Manchester United v Arsenal", Sequence = "12" });
            detail.Fixtures.Add(new FixtureOverview { Id = "3", IsStreaming = false, State = FixtureOverview.FixtureState.Over, Competition = "Champions League", CompetitionId = "AAAA", StartTime = new System.DateTime(2014, 3, 18, 20, 0, 0), Description = "Tottenham v Juventus", Sequence = "84" });
            detail.Fixtures.Add(new FixtureOverview { Id = "4", IsStreaming = false, State = FixtureOverview.FixtureState.Setup, IsInErrorState = true, Competition = "Serie A", CompetitionId = "823702", StartTime = new System.DateTime(2014, 2, 17, 9, 0, 0), Description = "Milan v Inter", LastException = "Mapping Exception", Sequence = "3" });
            detail.Fixtures.Add(new FixtureOverview { Id = "5", IsStreaming = false, State = FixtureOverview.FixtureState.Ready, Competition = "French Division 1", CompetitionId = "1qqqqqq", StartTime = new System.DateTime(2014, 3, 17, 17, 0, 0), Description = "PSG v Lion", Sequence = "99" });

            return detail;
        }

        
    }
}
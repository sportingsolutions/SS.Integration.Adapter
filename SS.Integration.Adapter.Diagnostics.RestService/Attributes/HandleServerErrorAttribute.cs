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
using System.Web.Http.Filters;
using log4net;
using System.Net;

namespace SS.Integration.Adapter.Diagnostics.RestService.Attributes
{
    public class HandleServerErrorAttribute : ExceptionFilterAttribute
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(HandleServerErrorAttribute));

        public override void OnException(HttpActionExecutedContext context)
        {
            _logger.Error("Adapter Supervisor - Error Handled");

            var ex = context.Exception;
            if (ex != null)
            {
                _logger.Error(ex);
                var request = context.Request;
                context.Response = request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }

            base.OnException(context);
        }
    }
}
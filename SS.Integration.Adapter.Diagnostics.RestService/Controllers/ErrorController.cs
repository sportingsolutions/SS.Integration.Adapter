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

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;

namespace SS.Integration.Adapter.Diagnostics.RestService.Controllers
{
    public class ErrorController : ApiController
    {
        private const string DEFAULT_REDIRECT_PAGE = "/index.html";

        /**
         * This allows to redirect the request to DEFAULT_REDIRECT_PAGE if the 
         * requested uri doesn't exist and it starts with "/ui". This is to
         * solve issues with client MVC frameworks that use logical urls (CMS style)
         * instead of physical urls (i.e /ui/sports when the physical path is /ui/partials/sports) and
         * the user copy and paste the url (hence, going outside the framework routing capabilities).
         * 
         * This controller will send out a 301 status code with "Location" header build as
         * DEFAULT_REDIRECT_PAGE + ?path=RequestedUri. (it is responsability of the client
         * re-initialise the framework routing capability (see /ui/js/app.js)
         *
         * It sends instead a standard 404 if the requested uri doesn't start with "/ui"
         * 
         */
        [HttpGet, HttpPost, HttpPut, HttpDelete, HttpHead, HttpOptions, AcceptVerbs("PATCH")]
        public HttpResponseMessage Handle404()
        {
            if (Service.ServiceInstance.ServiceConfiguration.UseUIRedirect)
            {

                if (Request.RequestUri.LocalPath.Contains(Service.ServiceInstance.ServiceConfiguration.UIPath))
                {
                    var response = Request.CreateResponse(HttpStatusCode.Redirect);
                    response.Headers.Location = BuildRedirectUrl(Request.RequestUri);

                    return response;
                }
            }

            var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                ReasonPhrase = "The requested resource is not found",

            };

            responseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(UrlUtilities.JSON_MEDIA_TYPE);
            return responseMessage;
        }

        private static Uri BuildRedirectUrl(Uri uri)
        {
            var basepath = uri.AbsoluteUri.Replace(uri.LocalPath, "");
            if (basepath.EndsWith("/"))
                basepath = basepath.Substring(0, basepath.Length - 1);

            basepath += Service.ServiceInstance.ServiceConfiguration.UIPath + DEFAULT_REDIRECT_PAGE;
            basepath += "?path=" + uri.LocalPath;
            return new Uri(basepath);
        }
    }
}
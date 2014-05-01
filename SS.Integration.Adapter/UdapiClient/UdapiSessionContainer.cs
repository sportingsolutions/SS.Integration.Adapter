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
using SportingSolutions.Udapi.Sdk;
using SportingSolutions.Udapi.Sdk.Interfaces;
using log4net;

namespace SS.Integration.Adapter.UdapiClient
{
    public class SessionContainer
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(SessionContainer));

        private static volatile ISession _theSession;
        private static readonly object SyncRoot = new Object();
        private readonly ICredentials _credentials;
        private readonly Uri _url;

        public SessionContainer(ICredentials credentials, Uri url)
        {
            _credentials = credentials;
            _url = url;
        }


        /// <summary>
        /// Returns a UDAPI session or throws
        /// an exception if it cannot connect
        /// to the service.
        /// 
        /// The session object is valid
        /// until ReleaseSession() is called
        /// </summary>
        public ISession Session
        {
            get
            {
                if (_theSession == null)
                {
                    lock (SyncRoot)
                    {
                        if (_theSession == null)
                        {
                            _logger.Info("Connecting to UDAPI....");
                            _theSession = SessionFactory.CreateSession(_url, _credentials);
                            _logger.Info("Successfully connected to UDAPI.");
                        }
                    }
                }

                return _theSession;
            }
        }

        /// <summary>
        /// Allows to release the current session.
        /// When the property Session is called,
        /// a new session will be established with
        /// the remote service
        /// </summary>
        public void ReleaseSession()
        {
            lock (SyncRoot)
            {
                _theSession = null;
            }
        }
    }
}

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
using System.Threading;
using SS.Integration.Adapter.Interface;
using SportingSolutions.Udapi.Sdk.Exceptions;
using log4net;

namespace SS.Integration.Adapter
{
    public class DefaultReconnectStrategy : IReconnectStrategy
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(Adapter).ToString());
        private readonly ISettings _settings;
        private Action<bool> _initialiseSession;

        public DefaultReconnectStrategy(ISettings settings)
        {
            _settings = settings;
        }

        public void SetSessionInitialiser(Action<bool> sessionInitialiser)
        {
            _initialiseSession = sessionInitialiser;
        }

        public void ReconnectOnException<T>(Action<T> ctx, T impl)
        {
            ReconnectOnException(x => { ctx(x); return new object(); }, impl);
        }

        public TResult ReconnectOnException<TResult, T>(Func<T, TResult> ctx, T impl)
        {
            var counter = 0;
            var reconnectSession = false;
            var retryDelay = _settings.StartingRetryDelay; //ms
            
            while (true)
            {
                try
                {
                    return ctx(impl);
                }
                catch (Exception ex)
                {
                    counter++;

                    if (ex is NotAuthenticatedException)
                    {
                        reconnectSession = true;
                    }

                    if (counter == _settings.MaxRetryAttempts)
                    {
                        _logger.Error(String.Format("There seems to be a persisting problem in executing Sporting Solutions method after {0} attempts", counter), ex);

                        throw;
                    }
                        
                    _logger.WarnFormat("Failed to successfully execute Sporting Solutions method on attempt {0}. Stack Trace:{1}", counter, ex.StackTrace);

                    retryDelay = 2 * retryDelay;
                    if (retryDelay > _settings.MaxRetryDelay)
                    {
                        retryDelay = _settings.MaxRetryDelay;
                    }

                    _logger.DebugFormat("Retrying Sporting Solutions API in {0} ms", retryDelay);

                    Thread.Sleep(retryDelay);

                    if (_initialiseSession != null)
                    {
                        _initialiseSession(reconnectSession);
                    }
                }
            }
        }
    }
}

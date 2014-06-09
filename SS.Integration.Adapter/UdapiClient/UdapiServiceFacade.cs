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
using System.Collections.Generic;
using System.Linq;
using SS.Integration.Adapter.Interface;
using SportingSolutions.Udapi.Sdk;
using SportingSolutions.Udapi.Sdk.Interfaces;
using log4net;
using System.Threading;
using SportingSolutions.Udapi.Sdk.Exceptions;

namespace SS.Integration.Adapter.UdapiClient
{
    public class UdapiServiceFacade : IServiceFacade
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(UdapiServiceFacade));

        private readonly SessionContainer _sessionContainer;
        private readonly ISettings _settings;
        private IService _service;
        private bool _isClosing;
        private readonly IReconnectStrategy _reconnectStrategy;

        public UdapiServiceFacade(IReconnectStrategy reconnectStrategy, ISettings settings)
        {
            _sessionContainer = new SessionContainer(new Credentials
            {
                UserName = settings.User,
                Password = settings.Password
            }, new Uri(settings.Url));

            _settings = settings;
            _isClosing = false;
            _reconnectStrategy = reconnectStrategy;
            _reconnectStrategy.SetSessionInitialiser(Init);
        }

        public void Connect()
        {
            try
            {
                _isClosing = false;
                IsConnected = false;
                Init(true);
            }
            catch (Exception)
            {
                _logger.Error("Unable to connect to the GTP-UDAPI");
                throw;
            }
        }

        public void Disconnect()
        {
            lock (this)
            {
                _isClosing = true;
                _service = null;
                IsConnected = false;
            }

            _logger.Info("Disconnect from GTP-UDAPI");
        }

        public bool IsConnected { get; private set; }

        public IEnumerable<IFeature> GetSports()
        {
            return _service == null ? null : _service.GetFeatures();
        }

        public List<IResourceFacade> GetResources(string featureName)
        {
            if (_service == null)
                return null;

            var resourceFacade = new List<IResourceFacade>();

            var udapiFeature = _service.GetFeature(featureName);

            if (udapiFeature != null)
            {
                var udapiResources = udapiFeature.GetResources();
                resourceFacade.AddRange(udapiResources.Select(udapiResource => new UdapiResourceFacade(udapiResource, featureName, _reconnectStrategy,_settings.EchoDelay,_settings.EchoInterval)));
            }

            return resourceFacade;
        }

        public IResourceFacade GetResource(string featureName, string resourceName)
        {
            if (_service == null)
                return null;

            IResourceFacade resource = null;

            var feature = _service.GetFeature(featureName);

            if (feature != null)
            {
                var udapiResource = feature.GetResource(resourceName);

                if (udapiResource != null)
                {
                    resource = new UdapiResourceFacade(udapiResource, featureName, _reconnectStrategy, _settings.EchoDelay, _settings.EchoInterval);
                }
            }

            return resource;
        }

        // TODO: Refactor!
        private void Init(bool connectSession)
        {
            var counter = 0;
            Exception lastException = null;
            
            var retryDelay = _settings.StartingRetryDelay; //ms

            while (counter < _settings.MaxRetryAttempts)
            {
                lock (this)
                {
                    if (_isClosing)
                    {                                                    
                        _service = null;
                        IsConnected = false;
                        return;
                    }
                }

                try
                {
                    if (connectSession)
                    {
                        _sessionContainer.ReleaseSession();
                    }

                    _service = _sessionContainer.Session.GetService("UnifiedDataAPI");

                    if (_service == null)
                    {
                        _logger.Fatal("Udapi Service proxy could not be created");
                    }

                    IsConnected = true;

                    return;
                }
                catch (NotAuthenticatedException wex)
                {
                    lastException = wex;
                    counter++;
                    if (counter == _settings.MaxRetryAttempts)
                    {
                        _logger.Error(
                              String.Format("Failed to successfully execute Sporting Solutions method after all {0} attempts",
                                            _settings.MaxRetryAttempts), wex);
                    }
                    else
                    {
                        _logger.WarnFormat("Failed to successfully execute Sporting Solutions method on attempt {0}. Stack Trace:{1}", counter, wex.StackTrace);
                    }

                    connectSession = true;
                }
                catch (Exception ex)
                {
                    counter++;
                    if (counter == _settings.MaxRetryAttempts)
                    {
                        _logger.Error(
                              String.Format("Failed to successfully execute Sporting Solutions method after all {0} attempts",
                                            _settings.MaxRetryAttempts), ex);
                    }
                    else
                    {
                        _logger.WarnFormat("Failed to successfully execute Sporting Solutions method on attempt {0}. Stack Trace:{1}", counter, ex.StackTrace);
                    }
                }

                retryDelay = 2 * retryDelay;
                if (retryDelay > _settings.MaxRetryDelay)
                {
                    retryDelay = _settings.MaxRetryDelay;
                }
                
                _logger.DebugFormat("Retrying Sporting Solutions API in {0} ms", retryDelay);
                
                Thread.Sleep(retryDelay);
            }

            throw lastException ?? new Exception();
        }
    }
}

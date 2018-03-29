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
        private readonly SessionContainer _reservedSessionContainer;

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
            _reservedSessionContainer = new SessionContainer(new Credentials
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


        public IResourceFacade GetResource(string featureName, string resourceName)
        {
            try
            {
                return _GetResource(featureName, resourceName);
            }
            catch (Exception ex)
            {
                ChangeSession(ex);
                return _GetResource(featureName, resourceName);
            }
        }

        public IEnumerable<IFeature> GetSports()
        {
            try
            {
                return _GetSports();
            }
            catch (Exception ex)
            {
                ChangeSession(ex);
                return _GetSports();
            }
        }

        public List<IResourceFacade> GetResources(string featureName)
        {
            try
            {
                return _GetResources(featureName);
            }
            catch (Exception ex)
            {
                ChangeSession(ex);
                return _GetResources(featureName);
            }
        }


        private IEnumerable<IFeature> _GetSports()
        {
            return _service?.GetFeatures();
        }

        private List<IResourceFacade> _GetResources(string featureName)
        {
            if (_service == null)
                return null;

            var resourceFacade = new List<IResourceFacade>();

            var udapiFeature = _service.GetFeature(featureName);

            if (udapiFeature != null)
            {
                var udapiResources = udapiFeature.GetResources();
                resourceFacade.AddRange(udapiResources.Select(udapiResource => new UdapiResourceFacade(udapiResource, featureName, _reconnectStrategy, _settings.EchoDelay, _settings.EchoInterval)));
            }

            return resourceFacade;
        }



        private IResourceFacade _GetResource(string featureName, string resourceName)
        {
            if (_service == null)
                return null;

            IResourceFacade resource = null;

            var feature = _service.GetFeature(featureName);

            var udapiResource = feature?.GetResource(resourceName);

            if (udapiResource != null)
            {
                resource = new UdapiResourceFacade(udapiResource, featureName, _reconnectStrategy, _settings.EchoDelay, _settings.EchoInterval);
            }

            return resource;
        }


        public void ChangeSession(Exception ex)
        {
            _logger.Warn($"UDAPI session changed, reason={ex}");
            _service = _reservedSessionContainer.Session.GetService("UnifiedDataAPI");
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
                        _reservedSessionContainer.ReleaseSession();
                    }

                    _service = _sessionContainer.Session.GetService("UnifiedDataAPI");

                    if (_service == null)
                    {
                        _logger.Fatal("Udapi Service proxy could not be created");
                    }
                    else
                    {
                        _service.IsServiceCacheEnabled = _settings.IsSdkServiceCacheEnabled;
                        _service.ServiceCacheInvalidationInterval = _settings.FixtureCheckerFrequency / 1000 - 2;
                    }

                    IsConnected = true;

                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    counter++;
                    string message =
                        $"{ex.GetType().Name}: Failed to init Udapi service  on attempt {counter}.";
                    if (counter == _settings.MaxRetryAttempts)
                    {
                        _logger.Error(message + Environment.NewLine + $"Stack Trace:{ex.StackTrace}");
                    }
                    else
                    {
                        _logger.Warn(message);
                    }

                    if (ex as NotAuthenticatedException != null)
                    {
                        connectSession = true;
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

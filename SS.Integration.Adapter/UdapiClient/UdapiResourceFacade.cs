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
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SportingSolutions.Udapi.Sdk.Events;
using log4net;

namespace SS.Integration.Adapter.UdapiClient
{
    public class UdapiResourceFacade : IResourceFacade, IStreamStatistics
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof(UdapiResourceFacade));
        
        private readonly IResource _udapiResource;

        private readonly string _featureName;

        private readonly IReconnectStrategy _reconnectStrategy;
        private readonly int _echoDelay;
        private readonly int _echoInterval;

       
        public UdapiResourceFacade(IResource udapiResource, string featureName, IReconnectStrategy reconnectStrategy, int echoDelay, int echoInterval)
        {
            _udapiResource = udapiResource;
            _featureName = featureName;
            _reconnectStrategy = reconnectStrategy;
            _echoDelay = echoDelay;
            _echoInterval = echoInterval;
        }

        #region IResourceFacade Implementation

        public string Id
        {
            get { return _reconnectStrategy.ReconnectOnException(x => x.Id, _udapiResource); }
        }

        public string Name
        {
            get { return _reconnectStrategy.ReconnectOnException(x => x.Name, _udapiResource); }
        }

        public string Sport
        {
            get { return _featureName; }
        }

        public Summary Content
        {
            get
            {
                if (_udapiResource.Content == null)
                {
                    return null;
                }

                return new Summary
                    {
                        Id = _udapiResource.Content.Id,
                        Date = _udapiResource.Content.Date,
                        MatchStatus = _udapiResource.Content.MatchStatus,
                        Sequence = _udapiResource.Content.Sequence,
                        StartTime = _udapiResource.Content.StartTime
                    };
            }
        }

        public bool IsMatchOver 
        {
            get
            {
                return Content != null && Content.MatchStatus == (int)MatchStatus.MatchOver;
            }
        }

        public string GetSnapshot()
        {
            try
            {
                return _reconnectStrategy.ReconnectOnException(x => x.GetSnapshot(), _udapiResource);
            }
            catch (Exception)
            {
                _logger.ErrorFormat("{0} : {1} - Unable to retrieve Snapshot from GTP-UDAPI after multiple attempts", _featureName, _udapiResource.Name);
                throw;
            }
        }

        public void StartStreaming()
        {
            StartStreaming(_echoInterval,_echoDelay);
        }

        public void StartStreaming(int echoInterval, int echoMaxDelay)
        {
            try
            {
                if (echoInterval == -1)
                {
                    _reconnectStrategy.ReconnectOnException(x => x.StartStreaming(), _udapiResource);
                }
                else
                {
                    _reconnectStrategy.ReconnectOnException(x => x.StartStreaming(echoInterval, echoMaxDelay), _udapiResource);    
                }
            }
            catch (Exception)
            {
                _logger.ErrorFormat("{0} : {1} - Unable to start streaming from GTP-UDAPI after multiple attempts", _featureName, _udapiResource.Name);
                throw;
            }
        }

        public void PauseStreaming()
        {
            try
            {
                _reconnectStrategy.ReconnectOnException(x => x.PauseStreaming(), _udapiResource);
            }
            catch (Exception)
            {
                _logger.ErrorFormat("{0} : {1} - Unable to pause streaming from GTP-UDAPI after multiple attempts", _featureName, _udapiResource.Name);
                throw;
            }
        }

        public void UnPauseStreaming()
        {
            try
            {
                _reconnectStrategy.ReconnectOnException(x => x.UnPauseStreaming(), _udapiResource);
            }
            catch (Exception)
            {
                _logger.ErrorFormat("{0} : {1} - Unable to un-pause streaming from GTP-UDAPI after multiple attempts", _featureName, _udapiResource.Name);
                throw;
            }
        }

        public void StopStreaming()
        {
            try
            {
                _reconnectStrategy.ReconnectOnException(x => x.StopStreaming(), _udapiResource);
            }
            catch (Exception)
            {
                _logger.ErrorFormat("{0} : {1} - Unable to stop streaming from GTP-UDAPI after multiple attempts", _featureName, _udapiResource.Name);
                throw;
            }
        }

        public MatchStatus MatchStatus
        {
            get
            {
                return Content != null ? (MatchStatus) Enum.Parse(typeof(MatchStatus),Content.MatchStatus.ToString()) : MatchStatus.NotApplicable;
            }
        }

        public override string ToString()
        {
            var format = "Fixture with fixtureId={0}";
            if (Name != null)
            {
                format += " fixtureName=\"{1}\"";
                return string.Format(format, Id, Name);
            }

            return string.Format(format, Id);
        }

        #endregion

        public event EventHandler StreamConnected
        {
            add
            {
                if(_udapiResource != null)
                    _udapiResource.StreamConnected += value;
            }
            remove
            {
                if (_udapiResource != null)
                    _udapiResource.StreamConnected -= value;
            }
        }

        public event EventHandler StreamDisconnected
        {
            add
            {
                if (_udapiResource != null)
                    _udapiResource.StreamDisconnected += value;
            }
            remove
            {
                if (_udapiResource != null)
                    _udapiResource.StreamDisconnected -= value;
            }
        }
        
        public event EventHandler<StreamEventArgs> StreamEvent
        {
            add
            {
                if (_udapiResource != null)
                    _udapiResource.StreamEvent += value;
            }
            remove
            {
                if (_udapiResource != null)
                    _udapiResource.StreamEvent -= value;
            }
        }


        #region IStreamStatistics Implementation

        public double EchoRoundTripInMilliseconds
        {
            get { return ((IStreamStatistics) _udapiResource).EchoRoundTripInMilliseconds; }
        }

        public bool IsStreamActive
        {
            get { return ((IStreamStatistics) _udapiResource).IsStreamActive; ; }
        }

        public DateTime LastMessageReceived
        {
            get { return ((IStreamStatistics) _udapiResource).LastMessageReceived; }
        }

        public DateTime LastStreamDisconnect
        {
            get { return ((IStreamStatistics) _udapiResource).LastStreamDisconnect; }
        }

        #endregion
    }
}

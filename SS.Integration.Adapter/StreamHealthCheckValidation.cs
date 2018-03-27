//Copyright 2017 Spin Services Limited

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
using log4net;
using SS.Integration.Adapter.Enums;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter
{
    /// <summary>
    /// This class has the responibility to validate the streaming conditions.
    /// e.g. Can we connect to the streaming server or have we missed processing sequences over a predefined threshold.
    /// </summary>
    public class StreamHealthCheckValidation : IStreamHealthCheckValidation
    {
        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamHealthCheckValidation).ToString());
        private readonly ISettings _settings;

        #endregion

        #region Constructors

        public StreamHealthCheckValidation(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        #endregion

        #region Implementation of IStreamHealthCheckValidation

        /// <summary>
        /// This method validates the streaming conditions
        /// check for resource sequence, match status, streaming state
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="state"></param>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public bool ValidateProcessedSequnce(IResourceFacade resource, StreamListenerState state, int sequence)
        {
            var rnd = new Random(200);
            if (rnd.Next() == 100)
            {
                _logger.Info($"ValidateProcessedSequnce=false for test purposes {resource}");
                return false;
            }


            if (resource.Content.Sequence - sequence < _settings.StreamSafetyThreshold)
                return true;

            if (ShouldIgnoreUnprocessedSequence(resource, state))
                return true;

            return false;
        }

        /// <summary>
        /// This method determines whether we can connect to the streaming server
        /// by checking current streaming state and match status
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool CanConnectToStreamServer(IResourceFacade resource, StreamListenerState state)
        {
            var isFixtureInSetup = resource.Content.MatchStatus == (int)MatchStatus.Setup;
            return
                state != StreamListenerState.Streaming &&
                (!isFixtureInSetup || _settings.AllowFixtureStreamingInSetupMode);
        }

        /// <summary>
        /// This method determines whether we should suspend the fixture when the stream disconnection occurs
        /// </summary>
        /// <param name="fixtureState"></param>
        /// <param name="fixtureStartTime"></param>
        /// <returns></returns>
        public bool ShouldSuspendOnDisconnection(FixtureState fixtureState, DateTime? fixtureStartTime)
        {
            if (fixtureState == null || !fixtureStartTime.HasValue)
                return true;

            var spanBetweenNowAndStartTime = fixtureStartTime.Value - DateTime.UtcNow;
            var doNotSuspend = _settings.DisablePrematchSuspensionOnDisconnection && spanBetweenNowAndStartTime.TotalMinutes > _settings.PreMatchSuspensionBeforeStartTimeInMins;
            return !doNotSuspend;
        }

        #endregion

        #region Private methods

        private bool ShouldIgnoreUnprocessedSequence(IResourceFacade resource, StreamListenerState state)
        {
            if (state != StreamListenerState.Streaming)
            {
                _logger.Debug($"ValidateProcessedSequnce skipped for {resource} Reason=\"Not Streaming\"");
                return true;
            }

            if (resource.Content.MatchStatus == (int)MatchStatus.Setup && !_settings.AllowFixtureStreamingInSetupMode)
            {
                _logger.Debug($"ValidateProcessedSequnce skipped for {resource} Reason=\"Fixture is in setup state\"");
                return true;
            }

            return false;
        }

        #endregion
    }
}

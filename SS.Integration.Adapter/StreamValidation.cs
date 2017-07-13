using System;
using log4net;
using SS.Integration.Adapter.Enums;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter
{
    public class StreamValidation : IStreamValidation
    {
        #region Attributes

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamValidation).ToString());
        private readonly ISettings _settings;

        #endregion

        #region Constructors

        public StreamValidation(ISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        #endregion

        #region Implementation of IStreamValidation

        public bool ValidateStream(IResourceFacade resource, StreamListenerState state, int sequence)
        {
            if (resource.Content.Sequence - sequence <= _settings.StreamSafetyThreshold)
                return true;

            if (ShouldIgnoreUnprocessedSequence(resource, state))
                return true;

            return false;
        }

        public bool CanConnectToStreamServer(IResourceFacade resource, StreamListenerState state)
        {
            var isFixtureInSetup = resource.Content.MatchStatus == (int)MatchStatus.Setup;
            return
                state != StreamListenerState.Streaming &&
                (!isFixtureInSetup || _settings.AllowFixtureStreamingInSetupMode);
        }

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

        private bool ShouldIgnoreUnprocessedSequence(IResourceFacade resource, Enums.StreamListenerState state)
        {
            if (state != Enums.StreamListenerState.Streaming)
            {
                _logger.Debug($"ValidateStream skipped for {resource} Reason=\"Not Streaming\"");
                return true;
            }

            if (resource.Content.MatchStatus == (int)MatchStatus.MatchOver)
            {
                _logger.Debug($"ValidateStream skipped for {resource} Reason=\"Match Is Over\"");
                return true;
            }

            if (resource.MatchStatus == MatchStatus.Setup && !_settings.AllowFixtureStreamingInSetupMode)
            {
                _logger.Debug($"ValidateStream skipped for {resource} Reason=\"Fixture is in setup state\"");
                return true;
            }

            return false;
        }

        #endregion
    }
}

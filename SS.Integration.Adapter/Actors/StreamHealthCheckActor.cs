using System;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Actors
{
    public class StreamHealthCheckActor : ReceiveActor
    {
        #region Constants

        public const string ActorName = nameof(StreamHealthCheckActor);

        #endregion

        #region Attributes

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamListenerActor).ToString());
        private readonly ISettings _settings;
        private readonly IResourceFacade _resource;
        private readonly IActorContext _streamListenerContext;
        private ICancelable _startStreamingNotResponding;
        private int _startStreamingNotRespondingWarnCount;

        #endregion

        #region Constructors

        public StreamHealthCheckActor(
            IResourceFacade resource,
            ISettings settings,
            IActorContext streamListenerContext)
        {
            _resource = resource ?? throw new ArgumentNullException(nameof(resource));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _streamListenerContext = streamListenerContext ?? throw new ArgumentNullException(nameof(streamListenerContext));

            Receive<ConnectToStreamServerMsg>(a => ConnectToStreamServerMsgHandler(a));
            Receive<StreamConnectedMsg>(a => StreamConnectedMsgHandler(a));
            Receive<StartStreamingNotRespondingMsg>(a => StartStreamingNotRespondingMsgHandler(a));
            Receive<StreamValidationMsg>(a => StreamValidationMsgHandler(a));
            Receive<FixtureValidationMsg>(a => FixtureValidationMsgHandler(a));
        }

        #endregion

        #region Message Handlers

        private void ConnectToStreamServerMsgHandler(ConnectToStreamServerMsg msg)
        {
            var interval = _settings.StartStreamingTimeoutInSeconds;
            if (interval <= 0)
                interval = 1;

            _startStreamingNotRespondingWarnCount = 0;
            _startStreamingNotResponding =
                Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                    interval * 1000,
                    interval * 1000,
                    Self,
                    new StartStreamingNotRespondingMsg { FixtureId = _resource.Id },
                    Self);
        }

        private void StreamConnectedMsgHandler(StreamConnectedMsg msg)
        {
            _startStreamingNotResponding?.Cancel();
            _startStreamingNotResponding = null;
        }

        private void StartStreamingNotRespondingMsgHandler(StartStreamingNotRespondingMsg msg)
        {
            var unresponsiveTime = ++_startStreamingNotRespondingWarnCount * _settings.StartStreamingTimeoutInSeconds;
            _logger.Warn(
                $"StartStreaming for {_resource} did't respond for {unresponsiveTime} seconds. " +
                "Possible network problem or port 5672 is locked");

            if (_startStreamingNotRespondingWarnCount > _settings.StartStreamingAttempts)
            {
                _startStreamingNotResponding.Cancel();
                _streamListenerContext.Parent.Tell(msg);
            }
        }

        private void StreamValidationMsgHandler(StreamValidationMsg msg)
        {
            if (ValidateStream(msg.Resource, msg.State, msg.CurrentSequence))
                Sender.Tell(true);

            _logger.Warn($"Detected invalid stream for resource {msg.Resource}");

            Sender.Tell(false);
        }

        private void FixtureValidationMsgHandler(FixtureValidationMsg msg)
        {
            msg.IsSequenceValid = IsSequenceValid(msg.FixtureDetla, msg.Sequence);
            msg.IsEpochValid = IsEpochValid(msg.FixtureDetla, msg.Epoch);

            Sender.Tell(msg);
        }

        #endregion

        #region Private methods

        private bool ValidateStream(IResourceFacade resource, StreamListenerActor.StreamListenerState state, int sequence)
        {
            if (resource.Content.Sequence - sequence <= _settings.StreamSafetyThreshold)
                return true;

            if (ShouldIgnoreUnprocessedSequence(resource, state))
                return true;

            return false;
        }

        private bool ShouldIgnoreUnprocessedSequence(IResourceFacade resource, StreamListenerActor.StreamListenerState state)
        {
            if (state != StreamListenerActor.StreamListenerState.Streaming)
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

        private bool IsSequenceValid(Fixture fixtureDelta, int sequence)
        {
            if (fixtureDelta.Sequence < sequence)
            {
                _logger.Debug(
                    $"fixture delta sequence={fixtureDelta.Sequence} is less than current sequence={sequence} in {fixtureDelta}");
                return false;
            }

            if (fixtureDelta.Sequence - sequence > 1)
            {
                _logger.Debug(
                    $"fixture delta sequence={fixtureDelta.Sequence} is more than one greater that current sequence={sequence} in {fixtureDelta} ");
                return false;
            }

            return true;
        }

        private bool IsEpochValid(Fixture fixtureDelta, int epoch)
        {
            if (fixtureDelta.Epoch < epoch)
            {
                _logger.Warn(
                    $"Unexpected fixture delta Epoch={fixtureDelta.Epoch} when current={epoch} for {fixtureDelta}");
                return false;
            }

            if (fixtureDelta.Epoch == epoch)
                return true;

            // Cases for fixtureDelta.Epoch > _currentEpoch
            _logger.Info(
                $"Epoch changed for {fixtureDelta} from={epoch} to={fixtureDelta.Epoch}");

            //the epoch change reason can contain multiple reasons
            if (fixtureDelta.IsStartTimeChanged && fixtureDelta.LastEpochChangeReason.Length == 1)
            {
                _logger.Info($"{fixtureDelta} has had its start time changed");
                return true;
            }

            return false;
        }

        #endregion
    }
}

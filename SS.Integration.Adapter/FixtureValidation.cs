using log4net;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter
{
    public class FixtureValidation : IFixtureValidation
    {
        #region Private members

        private readonly ILog _logger = LogManager.GetLogger(typeof(FixtureValidation).ToString());

        #endregion

        #region Implementation of IFixtureValidation

        public bool IsSequenceValid(Fixture fixtureDelta, int sequence)
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

        public bool IsEpochValid(Fixture fixtureDelta, int epoch)
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

        public bool IsSnapshotNeeded(IResourceFacade resourceFacade, FixtureState state)
        {
            _logger.Debug(
                $"{resourceFacade} has stored sequence={state?.Sequence}; resource sequence={resourceFacade?.Content?.Sequence}");

            return state == null ||
                   resourceFacade?.Content != null && resourceFacade.Content.Sequence != state.Sequence;
        }

        #endregion
    }
}

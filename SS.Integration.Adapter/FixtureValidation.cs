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

using log4net;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter
{
    /// <summary>
    /// This class has the responibility to validate the fixture (sequence, epoch, whether we need to retrieve and process a full snapshot)
    /// </summary>
    public class FixtureValidation : IFixtureValidation
    {
        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(FixtureValidation).ToString());

        #endregion

        #region Implementation of IFixtureValidation

        /// <summary>
        /// This method validates the fixture sequence.
        /// In order for the fixture sequence to be valid, it has to be greater with 1 than last processed sequence
        /// </summary>
        /// <param name="fixtureDelta"></param>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public bool IsNotMissedUpdates(Fixture fixtureDelta, int sequence)
        {
            if (fixtureDelta.Sequence - sequence > 1)
            {
                _logger.Debug(
                    $"fixture delta sequence={fixtureDelta.Sequence} is more than one greater that current sequence={sequence} in {fixtureDelta} ");
                return false;
            }

            return true;
        }


	    public bool IsSequnceActual(Fixture fixtureDelta, int sequence/*, bool isFullSnapshot*/)
	    {
			if (fixtureDelta.Sequence < sequence)
		    {
			    _logger.Debug(
				    $"fixture delta sequence={fixtureDelta.Sequence} is less than current sequence={sequence} in {fixtureDelta}");
			    return false;
		    }
		    return true;
		}

	    /// <summary>
        /// This method validates the fixture epoch.
        /// In order for the fixture epoch to be valid one of the following must be true:
        /// - current fixture epoch has to be the same as the last processed fixture epoch.
        /// - if current fixture epoch is greater than the last processed fixture epoch it must be only because of match start time has been changed.
        /// </summary>
        /// <param name="fixtureDelta"></param>
        /// <param name="epoch"></param>
        /// <returns></returns>
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

        /// <summary>
        /// This method checks whether we need to retrieve and process a new snapshot (when current resource sequence is different than te stored sequence).
        /// </summary>
        /// <param name="resourceFacade"></param>
        /// <param name="state"></param>
        /// <returns></returns>
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

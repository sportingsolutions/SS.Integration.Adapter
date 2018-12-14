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

using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class FixtureValidationTests
    {
        #region Constants

        public const string FIXTURE_VALIDATION_CATEGORY = nameof(FixtureValidation);

        #endregion

        #region Fields

        private Mock<IResourceFacade> _resourceFacadeMock;

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            _resourceFacadeMock = new Mock<IResourceFacade>();
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures we correctly validate sequence (return true - sequence is valid) when having 
        /// - delta fixture sequence 4
        /// - current sequence (the one already processed) 3
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenSequenceValidThenIsSequenceValidReturnsTrue()
        {
            //
            //Arrange
            //
            var fixtureDelta = new Fixture { Sequence = 4 };
            int currentSequence = 3;
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isSequenceValid = fixtureValidation.IsNotMissedUpdates(
                fixtureDelta,
                currentSequence);

            //
            //Assert
            //
            Assert.IsTrue(isSequenceValid);
        }

        /// <summary>
        /// This test ensures we correctly validate sequence (return false - sequence is not valid) when having 
        /// - delta fixture sequence 2
        /// - current sequence (the one already processed) 3
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenSequenceInvalidLowerThanCurrentSequenceThenIsSequenceValidReturnsFalse()
        {
            //
            //Arrange
            //
            var fixtureDelta = new Fixture { Sequence = 2 };
            int currentSequence = 3;
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isSequenceValid = fixtureValidation.IsSequnceActual(
                fixtureDelta,
                currentSequence);

            //
            //Assert
            //
            Assert.IsFalse(isSequenceValid);
        }

        /// <summary>
        /// This test ensures we correctly validate sequence (return false - sequence is not valid) when having 
        /// - delta fixture sequence 5
        /// - current sequence (the one already processed) 3
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenSequenceInvalidMoreThan1GreaterThanCurrentSequenceThenIsSequenceValidReturnsFalse()
        {
            //
            //Arrange
            //
            var fixtureDelta = new Fixture { Sequence = 5 };
            int currentSequence = 3;
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isSequenceValid = fixtureValidation.IsNotMissedUpdates(
                fixtureDelta,
                currentSequence);

            //
            //Assert
            //
            Assert.IsFalse(isSequenceValid);
        }

        /// <summary>
        /// This test ensures we correctly validate epoch (return true - epoch is valid) when having 
        /// - delta fixture epoch 4
        /// - current epoch (the one already processed) 4
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenEpochValidSameAsCurrentEpochThenIsEpochValidReturnsTrue()
        {
            //
            //Arrange
            //
            var fixtureDelta = new Fixture { Epoch = 4 };
            int currentEpoch = 4;
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isEpochValid = fixtureValidation.IsEpochValid(
                fixtureDelta,
                currentEpoch);

            //
            //Assert
            //
            Assert.IsTrue(isEpochValid);
        }

        /// <summary>
        /// This test ensures we correctly validate epoch (return true - epoch is valid) when having 
        /// - delta fixture epoch 4
        /// - current epoch (the one already processed) 3
        /// - multiple epoch change reasons, including start time changed
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenEpochValidGreaterThanCurrentEpochButOnlyBecauseOfStartTimeEpochChangeReasonThenIsEpochValidReturnsTrue()
        {
            //
            //Arrange
            //
            var fixtureDelta = new Fixture
            {
                Epoch = 4,
                LastEpochChangeReason = new[] { (int)EpochChangeReason.StartTime }
            };
            int currentEpoch = 3;
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isEpochValid = fixtureValidation.IsEpochValid(
                fixtureDelta,
                currentEpoch);

            //
            //Assert
            //
            Assert.IsTrue(isEpochValid);
        }

        /// <summary>
        /// This test ensures we correctly validate epoch (return false - epoch is not valid) when having 
        /// - delta fixture epoch 3
        /// - current epoch (the one already processed) 4
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenEpochInvalidLowerThanCurrentEpochThenIsEpochValidReturnsFalse()
        {
            //
            //Arrange
            //
            var fixtureDelta = new Fixture { Epoch = 3 };
            int currentEpoch = 4;
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isEpochValid = fixtureValidation.IsEpochValid(
                fixtureDelta,
                currentEpoch);

            //
            //Assert
            //
            Assert.IsFalse(isEpochValid);
        }

        /// <summary>
        /// This test ensures we correctly validate epoch (return false - epoch is not valid) when having 
        /// - delta fixture epoch 4
        /// - current epoch (the one already processed) 3
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenEpochInvalidGreaterThanCurrentEpochAndFixtureStartTimeChangedFalseThenIsEpochValidReturnsFalse()
        {
            //
            //Arrange
            //
            var fixtureDelta = new Fixture { Epoch = 4 };
            int currentEpoch = 3;
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isEpochValid = fixtureValidation.IsEpochValid(
                fixtureDelta,
                currentEpoch);

            //
            //Assert
            //
            Assert.IsFalse(isEpochValid);
        }

        /// <summary>
        /// This test ensures we correctly validate epoch (return false - epoch is not valid) when having 
        /// - delta fixture epoch 4
        /// - current epoch (the one already processed) 3
        /// - multiple epoch change reasons, including start time changed
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenEpochInvalidGreaterThanCurrentEpochAndMultipleEpochChangeReasonsThenIsEpochValidReturnsFalse()
        {
            //
            //Arrange
            //
            var fixtureDelta = new Fixture
            {
                Epoch = 4,
                LastEpochChangeReason = new[] { (int)EpochChangeReason.StartTime, (int)EpochChangeReason.Participants }
            };
            int currentEpoch = 3;
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isEpochValid = fixtureValidation.IsEpochValid(
                fixtureDelta,
                currentEpoch);

            //
            //Assert
            //
            Assert.IsFalse(isEpochValid);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we need to retrieve snapshot (return true - snapshot retrieval needed) when having 
        /// - fixture state null
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenStateIsNullThenIsSnapshotNeededReturnsTrue()
        {
            //
            //Arrange
            //
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isSnapshotNeeded = fixtureValidation.IsSnapshotNeeded(
                It.IsAny<IResourceFacade>(),
                null);

            //
            //Assert
            //
            Assert.IsTrue(isSnapshotNeeded);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we need to retrieve snapshot (return true - snapshot retrieval needed) when having 
        /// - fixture state is not null
        /// - fixture sequence is different than saved state sequence
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenSequenceDifferentThanStateSequenceThenIsSnapshotNeededReturnsTrue()
        {
            //
            //Arrange
            //
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(new Summary { Sequence = 4 });
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isSnapshotNeeded = fixtureValidation.IsSnapshotNeeded(
                _resourceFacadeMock.Object,
                new FixtureState { Sequence = 3 });

            //
            //Assert
            //
            Assert.IsTrue(isSnapshotNeeded);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we need to retrieve snapshot (return false - snapshot retrieval is not needed) when having 
        /// - fixture state not null
        /// - resource content null
        /// </summary>
        [Test]
        [Category(FIXTURE_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenStateIsNotNullAndContentNullThenIsSnapshotNeededReturnsFalse()
        {
            //
            //Arrange
            //
            var fixtureValidation = new FixtureValidation();

            //
            //Act
            //
            bool isSnapshotNeeded = fixtureValidation.IsSnapshotNeeded(
                _resourceFacadeMock.Object,
                new FixtureState());

            //
            //Assert
            //
            Assert.IsFalse(isSnapshotNeeded);
        }

        #endregion
    }
}

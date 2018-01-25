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
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Enums;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamHealthCheckValidationTests
    {
        #region Constants

        public const string STREAM_VALIDATION_CATEGORY = nameof(StreamHealthCheckValidation);

        #endregion

        #region Fields

        private Mock<ISettings> _settings;
        private Mock<IResourceFacade> _resourceFacadeMock;

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            _settings = new Mock<ISettings>();
            _resourceFacadeMock = new Mock<IResourceFacade>();
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures we correctly validate stream (return true - stream is valid) when having 
        /// - mock resource with sequence 3
        /// - current sequence (the one already processed) 2
        /// - stream safety threshold set to 5
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenSequenceValidThenValidateStreamReturnsTrue()
        {
            //
            //Arrange
            //
            var resourceSummary = new Summary { Sequence = 3 };
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(resourceSummary);
            int currentSequence = 2;
            _settings.SetupGet(s => s.StreamSafetyThreshold).Returns(5);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool streamIsValid = streamHealthCheckValidation.ValidateStream(
                _resourceFacadeMock.Object,
                It.IsAny<StreamListenerState>(),
                currentSequence);

            //
            //Assert
            //
            Assert.IsTrue(streamIsValid);
        }

        /// <summary>
        /// This test ensures we correctly validate stream (return true - stream is valid) when having 
        /// - mock resource with sequence 5
        /// - current sequence (the one already processed) 1
        /// - stream safety threshold set to 3
        /// - listener is not in streaming state
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenNotStreamingThenValidateStreamReturnsTrue()
        {
            //
            //Arrange
            //
            var resourceSummary = new Summary { Sequence = 5 };
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(resourceSummary);
            int currentSequence = 1;
            _settings.SetupGet(s => s.StreamSafetyThreshold).Returns(3);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool streamIsValid = streamHealthCheckValidation.ValidateStream(
                _resourceFacadeMock.Object,
                It.IsNotIn(StreamListenerState.Streaming),
                currentSequence);

            //
            //Assert
            //
            Assert.IsTrue(streamIsValid);
        }

        /// <summary>
        /// This test ensures we correctly validate stream (return false - stream is not valid) when having 
        /// - mock resource with sequence 5
        /// - current sequence (the one already processed) 1
        /// - stream safety threshold set to 3
        /// - listener is in streaming state
        /// - match is over
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenMatchOverThenValidateStreamReturnsFalse()
        {
            //
            //Arrange
            //
            var resourceSummary = new Summary { Sequence = 5, MatchStatus = (int)MatchStatus.MatchOver };
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(resourceSummary);
            int currentSequence = 1;
            _settings.SetupGet(s => s.StreamSafetyThreshold).Returns(3);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool streamIsValid = streamHealthCheckValidation.ValidateStream(
                _resourceFacadeMock.Object,
                StreamListenerState.Streaming,
                currentSequence);

            //
            //Assert
            //
            Assert.IsFalse(streamIsValid);
        }

        /// <summary>
        /// This test ensures we correctly validate stream (return true - stream is valid) when having 
        /// - mock resource with sequence 5
        /// - current sequence (the one already processed) 1
        /// - stream safety threshold set to 3
        /// - listener is in streaming state
        /// - match is in setup
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenInSetupAndNotAllowedStreamingThenValidateStreamReturnsTrue()
        {
            //
            //Arrange
            //
            var resourceSummary = new Summary { Sequence = 5, MatchStatus = (int)MatchStatus.Setup };
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(resourceSummary);
            int currentSequence = 1;
            _settings.SetupGet(s => s.StreamSafetyThreshold).Returns(3);
            _settings.SetupGet(s => s.AllowFixtureStreamingInSetupMode).Returns(false);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool streamIsValid = streamHealthCheckValidation.ValidateStream(
                _resourceFacadeMock.Object,
                StreamListenerState.Streaming,
                currentSequence);

            //
            //Assert
            //
            Assert.IsTrue(streamIsValid);
        }

        /// <summary>
        /// This test ensures we correctly validate stream (return false - stream is not valid) when having 
        /// - mock resource with sequence 5
        /// - current sequence (the one already processed) 1
        /// - stream safety threshold set to 3
        /// - listener is in streaming state
        /// - match is not over
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenNotOverAndStreamingThenValidateStreamReturnsFalse()
        {
            //
            //Arrange
            //
            var resourceSummary = new Summary { Sequence = 5, MatchStatus = (int)It.IsNotIn(MatchStatus.MatchOver) };
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(resourceSummary);
            int currentSequence = 1;
            _settings.SetupGet(s => s.StreamSafetyThreshold).Returns(3);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool streamIsValid = streamHealthCheckValidation.ValidateStream(
                _resourceFacadeMock.Object,
                StreamListenerState.Streaming,
                currentSequence);

            //
            //Assert
            //
            Assert.IsFalse(streamIsValid);
        }

        /// <summary>
        /// This test ensures we correctly validate stream (return false - stream is not valid) when having 
        /// - mock resource with sequence 5
        /// - current sequence (the one already processed) 1
        /// - stream safety threshold set to 3
        /// - listener is in streaming state
        /// - match is in setup and streaming is allowed in setup mode
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenInSetupAndStreamingThenValidateStreamReturnsFalse()
        {
            //
            //Arrange
            //
            var resourceSummary = new Summary { Sequence = 5, MatchStatus = (int)MatchStatus.Setup };
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(resourceSummary);
            int currentSequence = 1;
            _settings.SetupGet(s => s.StreamSafetyThreshold).Returns(3);
            _settings.SetupGet(s => s.AllowFixtureStreamingInSetupMode).Returns(true);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool streamIsValid = streamHealthCheckValidation.ValidateStream(
                _resourceFacadeMock.Object,
                StreamListenerState.Streaming,
                currentSequence);

            //
            //Assert
            //
            Assert.IsFalse(streamIsValid);
        }

        /// <summary>
        /// This test ensures we correctly validate stream (return false - stream is not valid) when having 
        /// - mock resource with sequence 5
        /// - current sequence (the one already processed) 1
        /// - stream safety threshold set to 3
        /// - listener is in streaming state
        /// - match is not in setup and is not over
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenNotOverAndNotInSetupAndStreamingThenValidateStreamReturnsFalse()
        {
            //
            //Arrange
            //
            var resourceSummary = new Summary
            {
                Sequence = 5,
                MatchStatus = (int)It.IsNotIn(MatchStatus.Setup, MatchStatus.MatchOver)
            };
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(resourceSummary);
            int currentSequence = 1;
            _settings.SetupGet(s => s.StreamSafetyThreshold).Returns(3);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool streamIsValid = streamHealthCheckValidation.ValidateStream(
                _resourceFacadeMock.Object,
                StreamListenerState.Streaming,
                currentSequence);

            //
            //Assert
            //
            Assert.IsFalse(streamIsValid);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we can connect to the stream server (returns true) when having 
        /// - mock resource with MatchStatus not in Setup
        /// - listener is not in streaming state
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenNotInSetupAndNotStreamingThenCanConnectToStreamServerReturnsTrue()
        {
            //
            //Arrange
            //
            var resourceSummary = new Summary { MatchStatus = (int)It.IsNotIn(MatchStatus.Setup) };
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(resourceSummary);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool canConnectToStreamServer = streamHealthCheckValidation.CanConnectToStreamServer(
                _resourceFacadeMock.Object,
                It.IsNotIn(StreamListenerState.Streaming));

            //
            //Assert
            //
            Assert.IsTrue(canConnectToStreamServer);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we can connect to the stream server (returns true) when having 
        /// - mock resource with MatchStatus in Setup
        /// - listener is not in streaming state
        /// - allowed streaming in Setup mode
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenInSetupAndAllowedStreamingThenCanConnectToStreamServerReturnsTrue()
        {
            //
            //Arrange
            //
            var resourceSummary = new Summary { MatchStatus = (int)MatchStatus.Setup };
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(resourceSummary);
            _settings.SetupGet(o => o.AllowFixtureStreamingInSetupMode).Returns(true);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool canConnectToStreamServer = streamHealthCheckValidation.CanConnectToStreamServer(
                _resourceFacadeMock.Object,
                It.IsNotIn(StreamListenerState.Streaming));

            //
            //Assert
            //
            Assert.IsTrue(canConnectToStreamServer);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we can connect to the stream server (returns false) when having 
        /// - listener is in streaming state
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenStreamingThenCanConnectToStreamServerReturnsFalse()
        {
            //
            //Arrange
            //
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(new Summary());
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool canConnectToStreamServer = streamHealthCheckValidation.CanConnectToStreamServer(
                _resourceFacadeMock.Object,
                StreamListenerState.Streaming);

            //
            //Assert
            //
            Assert.IsFalse(canConnectToStreamServer);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we can connect to the stream server (returns false) when having 
        /// - mock resource with MatchStatus in Setup
        /// - listener is not in streaming state
        /// - not allowed streaming in setup mode
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenInSetupAndNotAllowedStreamingThenCanConnectToStreamServerReturnsFalse()
        {
            //
            //Arrange
            //
            var resourceSummary = new Summary { MatchStatus = (int)MatchStatus.Setup };
            _resourceFacadeMock.SetupGet(o => o.Content).Returns(resourceSummary);
            _settings.SetupGet(o => o.AllowFixtureStreamingInSetupMode).Returns(false);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool canConnectToStreamServer = streamHealthCheckValidation.CanConnectToStreamServer(
                _resourceFacadeMock.Object,
                It.IsNotIn(StreamListenerState.Streaming));

            //
            //Assert
            //
            Assert.IsFalse(canConnectToStreamServer);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we should suspend fixture on disconnection (returns true) when having 
        /// - fixture state null
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureStateNullShouldSuspendOnDisconnectionReturnsTrue()
        {
            //
            //Arrange
            //
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool shouldSuspendOnDisconnection = streamHealthCheckValidation.ShouldSuspendOnDisconnection(
                null,
                It.IsAny<DateTime?>());

            //
            //Assert
            //
            Assert.IsTrue(shouldSuspendOnDisconnection);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we should suspend fixture on disconnection (returns true) when having 
        /// - fixture state is not null
        /// - fixture start time is null
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureStartTimeNullShouldSuspendOnDisconnectionReturnsTrue()
        {
            //
            //Arrange
            //
            var fixtureState = new FixtureState();
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool shouldSuspendOnDisconnection = streamHealthCheckValidation.ShouldSuspendOnDisconnection(
                fixtureState, 
                null);

            //
            //Assert
            //
            Assert.IsTrue(shouldSuspendOnDisconnection);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we should suspend fixture on disconnection (returns true) when having 
        /// - fixture state is not null
        /// - fixture start time is not null
        /// - DisablePrematchSuspensionOnDisconnection setting is false
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureStateNotNullAndStartTimeNotNullShouldSuspendOnDisconnectionReturnsTrue()
        {
            //
            //Arrange
            //
            var fixtureState = new FixtureState();
            _settings.SetupGet(o => o.DisablePrematchSuspensionOnDisconnection).Returns(false);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool shouldSuspendOnDisconnection = streamHealthCheckValidation.ShouldSuspendOnDisconnection(
                fixtureState,
                It.IsAny<DateTime>());

            //
            //Assert
            //
            Assert.IsTrue(shouldSuspendOnDisconnection);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we should suspend fixture on disconnection (returns false) when having 
        /// - fixture state is not null
        /// - fixture start time is set to 5 minutes over the current test execution time
        /// - DisablePrematchSuspensionOnDisconnection setting is true
        /// - PreMatchSuspensionBeforeStartTimeInMins is 10
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureStateAndStartTimeAreValidThenShouldSuspendOnDisconnectionReturnsTrue()
        {
            //
            //Arrange
            //
            var fixtureState = new FixtureState();
            _settings.SetupGet(o => o.DisablePrematchSuspensionOnDisconnection).Returns(true);
            _settings.SetupGet(o => o.PreMatchSuspensionBeforeStartTimeInMins).Returns(10);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool shouldSuspendOnDisconnection = streamHealthCheckValidation.ShouldSuspendOnDisconnection(
                fixtureState,
                DateTime.UtcNow.AddMinutes(5));

            //
            //Assert
            //
            Assert.IsTrue(shouldSuspendOnDisconnection);
        }

        /// <summary>
        /// This test ensures we correctly evaluate wether we should suspend fixture on disconnection (returns false) when having 
        /// - fixture state is not null
        /// - fixture start time is set to 10 minutes over the current test execution time
        /// - DisablePrematchSuspensionOnDisconnection setting is true
        /// - PreMatchSuspensionBeforeStartTimeInMins is 5
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureStateAndStartTimeAreInvalidThenShouldSuspendOnDisconnectionReturnsFalse()
        {
            //
            //Arrange
            //
            var fixtureState = new FixtureState();
            _settings.SetupGet(o => o.DisablePrematchSuspensionOnDisconnection).Returns(true);
            _settings.SetupGet(o => o.PreMatchSuspensionBeforeStartTimeInMins).Returns(5);
            var streamHealthCheckValidation = new StreamHealthCheckValidation(_settings.Object);

            //
            //Act
            //
            bool shouldSuspendOnDisconnection = streamHealthCheckValidation.ShouldSuspendOnDisconnection(
                fixtureState,
                DateTime.UtcNow.AddMinutes(10));

            //
            //Assert
            //
            Assert.IsFalse(shouldSuspendOnDisconnection);
        }

        #endregion
    }
}

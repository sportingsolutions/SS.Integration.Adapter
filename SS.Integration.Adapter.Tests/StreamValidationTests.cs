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

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamValidationTests
    {
        #region Constants

        public const string STREAM_VALIDATION_CATEGORY = nameof(StreamValidation);

        #endregion

        #region Attributes

        private Mock<ISettings> _settings;

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            _settings = new Mock<ISettings>();
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures we move to Streaming State after processing health check message.
        /// Also Snapshot processing is done twice 
        /// - first snapshot processing is done on initialization due to different sequence numbers between stored and current
        /// - second snapshot processing is done after we process the health check message due to match status changed
        /// </summary>
        [Test]
        [Category(STREAM_VALIDATION_CATEGORY)]
        public void GivenFixtureWhenSequence3AndCurrentSequence2AndStreamSafetyThreshold5ThenStreamValidationReturnsTrue()
        {
            //
            //Arrange
            //
            Fixture fixture = new Fixture { Sequence = 3 };
            int currentSequence = 2;
            _settings.SetupGet(s => s.StreamSafetyThreshold).Returns(5);
            //var streamValidation = new StreamValidation();

            //
            //Act
            //


            //
            //Assert
            //
        }

        #endregion
    }
}

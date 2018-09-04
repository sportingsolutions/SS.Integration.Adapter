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
using System.Threading.Tasks;
using Akka.Actor;
using NUnit.Framework;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class StreamStatsActorTests : AdapterTestKit
    {
        #region Constants

        public const string STREAM_STATS_ACTOR_CATEGORY = nameof(StreamStatsActor);

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures we correctly increment snapshot count on Update Stats message
        /// </summary>
        [Test]
        [Category(STREAM_STATS_ACTOR_CATEGORY)]
        public void TestUpdateStatsSnapshotCount()
        {
            //
            //Arrange
            //
            var streamStatsActorRef =
                ActorOfAsTestActorRef<StreamStatsActor>(
                    Props.Create(() => new StreamStatsActor()),
                    StreamStatsActor.ActorName);
            var dateNow = DateTime.UtcNow;
            var updateStatsStartMsg =
                new AdapterProcessingStarted
                {
                    UpdateReceivedAt = dateNow,
                    Sequence = 1,
                    IsSnapshot = true,
                    Fixture = new Fixture
                    {
                        Id = "Fixture1Id",
                        Sequence = 1,
                        Epoch = 1,
                        MatchStatus = MatchStatus.Prematch.ToString()
                    }
                };
            var updateStatsFinishMsg = 
                new AdapterProcessingFinished
                {
                    CompletedAt = dateNow.AddMilliseconds(1500)
                };

            //
            //Act
            //
            streamStatsActorRef.Tell(updateStatsStartMsg);
            Task.Delay(TimeSpan.FromMilliseconds(500));
            streamStatsActorRef.Tell(updateStatsFinishMsg);
            Task.Delay(TimeSpan.FromMilliseconds(500));
            streamStatsActorRef.Tell(updateStatsStartMsg);
            Task.Delay(TimeSpan.FromMilliseconds(500));
            streamStatsActorRef.Tell(updateStatsFinishMsg);

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    Assert.AreEqual(2, streamStatsActorRef.UnderlyingActor.SnapshotsCount);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.StreamUpdatesCount);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.DisconnectionsCount);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.ErrorsCount[StreamStatsActor.PluginExceptionType]);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.ErrorsCount[StreamStatsActor.ApiExceptionType]);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.ErrorsCount[StreamStatsActor.GenericExceptionType]);
                    Assert.AreEqual(DateTime.MinValue, streamStatsActorRef.UnderlyingActor.LastDisconnectedDate);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we correctly increment stream updates count on Update Stats message
        /// </summary>
        [Test]
        [Category(STREAM_STATS_ACTOR_CATEGORY)]
        public void TestUpdateStatsStreamUpdatesCount()
        {
            //
            //Arrange
            //
            var streamStatsActorRef =
                ActorOfAsTestActorRef<StreamStatsActor>(
                    Props.Create(() => new StreamStatsActor()),
                    StreamStatsActor.ActorName);
            var dateNow = DateTime.UtcNow;
            var updateStatsStartMsg =
                new AdapterProcessingStarted
                {
                    UpdateReceivedAt = dateNow,
                    Sequence = 1,
                    IsSnapshot = false,
                    Fixture = new Fixture
                    {
                        Id = "Fixture1Id",
                        Sequence = 1,
                        Epoch = 1,
                        MatchStatus = MatchStatus.Prematch.ToString()
                    }
                };
            var updateStatsFinishMsg =
                new AdapterProcessingFinished
                {
                    CompletedAt = dateNow.AddMilliseconds(1500)
                };

            //
            //Act
            //
            streamStatsActorRef.Tell(updateStatsStartMsg);
            Task.Delay(TimeSpan.FromMilliseconds(500));
            streamStatsActorRef.Tell(updateStatsFinishMsg);
            Task.Delay(TimeSpan.FromMilliseconds(500));
            streamStatsActorRef.Tell(updateStatsStartMsg);
            Task.Delay(TimeSpan.FromMilliseconds(500));
            streamStatsActorRef.Tell(updateStatsFinishMsg);

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.SnapshotsCount);
                    Assert.AreEqual(2, streamStatsActorRef.UnderlyingActor.StreamUpdatesCount);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.DisconnectionsCount);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.ErrorsCount[StreamStatsActor.PluginExceptionType]);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.ErrorsCount[StreamStatsActor.ApiExceptionType]);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.ErrorsCount[StreamStatsActor.GenericExceptionType]);
                    Assert.AreEqual(DateTime.MinValue, streamStatsActorRef.UnderlyingActor.LastDisconnectedDate);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        /// <summary>
        /// This test ensures we correctly increment stream disconnections count on Update Stats message
        /// </summary>
        [Test]
        [Category(STREAM_STATS_ACTOR_CATEGORY)]
        public void TestUpdateStatsStreamDisconnectionsCount()
        {
            //
            //Arrange
            //
            var streamStatsActorRef =
                ActorOfAsTestActorRef<StreamStatsActor>(
                    Props.Create(() => new StreamStatsActor()),
                    StreamStatsActor.ActorName);
            var streamDisconnectedMsg = new StreamDisconnectedMsg();

            //
            //Act
            //
            streamStatsActorRef.Tell(streamDisconnectedMsg);
            Task.Delay(TimeSpan.FromMilliseconds(500));
            streamStatsActorRef.Tell(streamDisconnectedMsg);

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.SnapshotsCount);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.StreamUpdatesCount);
                    Assert.AreEqual(2, streamStatsActorRef.UnderlyingActor.DisconnectionsCount);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.ErrorsCount[StreamStatsActor.PluginExceptionType]);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.ErrorsCount[StreamStatsActor.ApiExceptionType]);
                    Assert.AreEqual(0, streamStatsActorRef.UnderlyingActor.ErrorsCount[StreamStatsActor.GenericExceptionType]);
                    Assert.Less((DateTime.UtcNow - streamStatsActorRef.UnderlyingActor.LastDisconnectedDate).TotalSeconds, 5);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        #endregion
    }
}

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
using System.Collections.Generic;
using Akka.Actor;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.Diagnostics.Actors;
using SS.Integration.Adapter.Diagnostics.Model;
using ServiceInterface = SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using ServiceModelInterface = SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;
using ServiceModel = SS.Integration.Adapter.Diagnostics.Model.Service.Model;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class SupervisorActorTests : BaseTestKit
    {
        #region Constants

        public const string SUPERVISOR_ACTOR_CATEGORY = nameof(SupervisorActor);

        #endregion

        #region Properties

        protected Mock<ServiceInterface.ISupervisorStreamingService> SupervisorStreamingServiceMock;
        protected Mock<IObjectProvider<Dictionary<string, FixtureOverview>>> ObjectProviderMock;

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            SupervisorStreamingServiceMock = new Mock<ServiceInterface.ISupervisorStreamingService>();
            ObjectProviderMock = new Mock<IObjectProvider<Dictionary<string, FixtureOverview>>>();
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
        [Category(SUPERVISOR_ACTOR_CATEGORY)]
        public void UpdateSupervisorStateIsProcessed()
        {
            //
            //Arrange
            //
            var updateSupervisorStateMsg = new UpdateSupervisorStateMsg
            {
                FixtureId = "fixture1Id",
                Sport = "sport1",
                Epoch = 1,
                CurrentSequence = 1,
                StartTime = DateTime.UtcNow.AddDays(-1),
                IsSnapshot = true,
                CompetitionId = "competition1Id",
                CompetitionName = "competition1Name",
                Name = "name1",
                MatchStatus = MatchStatus.Prematch,
                LastEpochChangeReason = null,
                IsStreaming = true,
                IsErrored = false,
                IsSuspended = false,
                Exception = null
            };
            var supervisorActorRef =
                ActorOfAsTestActorRef<SupervisorActor>(
                    Props.Create(() =>
                        new SupervisorActor(
                            SupervisorStreamingServiceMock.Object,
                            ObjectProviderMock.Object)),
                    SupervisorActor.ActorName);
            var supervisorActor = supervisorActorRef.UnderlyingActor;

            //
            //Act
            //
            supervisorActorRef.Tell(updateSupervisorStateMsg);

            //
            //Assert
            //
            AwaitAssert(() =>
                {
                    Assert.AreEqual(1, supervisorActor.FixturesOverview.Count);
                    Assert.AreEqual(1, supervisorActor.SportsOverview.Count);
                    Assert.IsTrue(supervisorActor.FixturesOverview.ContainsKey(updateSupervisorStateMsg.FixtureId));
                    Assert.IsTrue(supervisorActor.SportsOverview.ContainsKey(updateSupervisorStateMsg.Sport));
                    var fixtureOverview = supervisorActor.FixturesOverview[updateSupervisorStateMsg.FixtureId];
                    var sportOverview = supervisorActor.SportsOverview[updateSupervisorStateMsg.Sport];
                    Assert.IsNotNull(fixtureOverview.ListenerOverview);
                    Assert.AreEqual(updateSupervisorStateMsg.FixtureId, fixtureOverview.Id);
                    Assert.AreEqual(updateSupervisorStateMsg.Sport, fixtureOverview.Sport);
                    Assert.AreEqual(updateSupervisorStateMsg.Epoch, fixtureOverview.ListenerOverview.Epoch);
                    Assert.AreEqual(updateSupervisorStateMsg.CurrentSequence, fixtureOverview.ListenerOverview.Sequence);
                    Assert.AreEqual(updateSupervisorStateMsg.StartTime, fixtureOverview.ListenerOverview.StartTime);
                    Assert.AreEqual(updateSupervisorStateMsg.CompetitionId, fixtureOverview.CompetitionId);
                    Assert.AreEqual(updateSupervisorStateMsg.CompetitionName, fixtureOverview.CompetitionName);
                    Assert.AreEqual(updateSupervisorStateMsg.Name, fixtureOverview.Name);
                    Assert.AreEqual(updateSupervisorStateMsg.MatchStatus, fixtureOverview.ListenerOverview.MatchStatus);
                    Assert.AreEqual(updateSupervisorStateMsg.LastEpochChangeReason, fixtureOverview.ListenerOverview.LastEpochChangeReason);
                    Assert.AreEqual(updateSupervisorStateMsg.IsStreaming, fixtureOverview.ListenerOverview.IsStreaming);
                    Assert.AreEqual(updateSupervisorStateMsg.IsErrored, fixtureOverview.ListenerOverview.IsErrored);
                    Assert.AreEqual(updateSupervisorStateMsg.IsSuspended, fixtureOverview.ListenerOverview.IsSuspended);
                    Assert.AreEqual(updateSupervisorStateMsg.IsOver, fixtureOverview.ListenerOverview.IsOver);
                    Assert.IsNull(fixtureOverview.LastError);
                    Assert.AreEqual(updateSupervisorStateMsg.Sport, sportOverview.Name);
                    Assert.AreEqual(1, sportOverview.Total);
                    Assert.AreEqual(0, sportOverview.InPlay);
                    Assert.AreEqual(0, sportOverview.InSetup);
                    Assert.AreEqual(1, sportOverview.InPreMatch);
                    Assert.AreEqual(0, sportOverview.InErrorState);

                    SupervisorStreamingServiceMock.Verify(a =>
                            a.OnFixtureUpdate(It.IsAny<ServiceModelInterface.IFixtureDetails>()),
                        Times.Once);
                    SupervisorStreamingServiceMock.Verify(a =>
                            a.OnFixtureUpdate(It.Is<ServiceModelInterface.IFixtureDetails>(f =>
                                f.Id.Equals(fixtureOverview.Id) &&
                                f.IsDeleted.Equals(fixtureOverview.ListenerOverview.IsDeleted) &&
                                f.IsOver.Equals(fixtureOverview.ListenerOverview.IsOver) &&
                                f.IsStreaming.Equals(fixtureOverview.ListenerOverview.IsStreaming
                                    .GetValueOrDefault()) &&
                                f.IsInErrorState.Equals(fixtureOverview.ListenerOverview.IsErrored
                                    .GetValueOrDefault()) &&
                                f.StartTime.Equals(fixtureOverview.ListenerOverview.StartTime.GetValueOrDefault()) &&
                                f.Competition.Equals(fixtureOverview.CompetitionName) &&
                                f.CompetitionId.Equals(fixtureOverview.CompetitionId) &&
                                f.Description.Equals(fixtureOverview.Name) &&
                                f.State.Equals(ServiceModelInterface.FixtureState.PreMatch))),
                        Times.Once);
                    SupervisorStreamingServiceMock.Verify(a =>
                            a.OnSportUpdate(It.IsAny<ServiceModelInterface.ISportDetails>()),
                        Times.Once);
                    SupervisorStreamingServiceMock.Verify(a =>
                            a.OnSportUpdate(It.Is<ServiceModelInterface.ISportDetails>(s =>
                                s.Name.Equals(sportOverview.Name) &&
                                s.InErrorState.Equals(sportOverview.InErrorState) &&
                                s.InPlay.Equals(sportOverview.InPlay) &&
                                s.InPreMatch.Equals(sportOverview.InPreMatch) &&
                                s.InSetup.Equals(sportOverview.InSetup) &&
                                s.Total.Equals(sportOverview.Total))),
                        Times.Once);
                },
                TimeSpan.FromMilliseconds(ASSERT_WAIT_TIMEOUT),
                TimeSpan.FromMilliseconds(ASSERT_EXEC_INTERVAL));
        }

        #endregion
    }
}

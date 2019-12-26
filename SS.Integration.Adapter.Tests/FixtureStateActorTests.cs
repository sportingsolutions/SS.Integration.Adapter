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
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Akka.Actor;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using Newtonsoft.Json;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class FixtureStateActorTests : AdapterTestKit
    {
        #region Constants

        public const string FIXTURE_STATE_ACTOR_CATEGORY = nameof(FixtureStateActorTests);

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            SetupTestLogging();
            
            SettingsMock = new Mock<ISettings>();
            var location = Assembly.GetExecutingAssembly().Location ;
            var directory = Path.GetDirectoryName(location);
            string fileName = @"Data\fixtureStates.json";
            var fullName = Path.Combine(directory, fileName);
            var fi = new  FileInfo(fullName);
            var fixtureStatesFilePath = fi.FullName;
            Assert.IsTrue(File.Exists(fixtureStatesFilePath));
            SettingsMock.SetupGet(a => a.StateProviderPath).Returns(GetType().Assembly.Location);
            SettingsMock.SetupGet(a => a.FixturesStateFilePath).Returns(fixtureStatesFilePath);
            SettingsMock.SetupGet(a => a.FixturesStateAutoStoreInterval).Returns(1000);

            StoreProviderMock = new Mock<IStoreProvider>();
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures the GetFixtureStateMsg returns the fixture state as expected
        /// </summary>
        [Test]
        [Category(FIXTURE_STATE_ACTOR_CATEGORY)]
        public void GivenExistingFixtureStateWhenSendGetFixtureStateMsgThenReturnsTheFixtureState()
        {
            //
            //Arrange
            //
            var storedFixtureState = new FixtureState
            {
                Id = "id1",
                Sport = "sport1",
                Epoch = 1,
                Sequence = 1,
                MatchStatus = MatchStatus.Setup
            };
            StoreProviderMock.Setup(o => o.Read(It.IsAny<string>()))
                .Returns(JsonConvert.SerializeObject(
                    new Dictionary<string, FixtureState> { { storedFixtureState.Id, storedFixtureState } },
                    Formatting.Indented));
            var fixtureStateActor = ActorOfAsTestActorRef<FixtureStateActor>(
                Props.Create(() =>
                    new FixtureStateActor(
                        SettingsMock.Object,
                        StoreProviderMock.Object)),
                FixtureStateActor.ActorName);

            //
            //Act
            //
            var fixtureState =
                fixtureStateActor
                    .Ask<FixtureState>(new GetFixtureStateMsg { FixtureId = storedFixtureState.Id })
                    .Result;

            //
            //Assert
            //
            Assert.AreEqual(storedFixtureState, fixtureState);
        }

        /// <summary>
        /// This test ensures the UpdateFixtureStateMsg updates the fixture state as expected
        /// </summary>
        [Test]
        [Category(FIXTURE_STATE_ACTOR_CATEGORY)]
        public void GivenEmptyStateWhenSendUpdateFixtureStateMsgThenItUpdatedTheStateInMemory()
        {
            //
            //Arrange
            //
            var storedFixtureState = new FixtureState
            {
                Id = "id1",
                Sport = "sport1",
                Epoch = 1,
                Sequence = 1,
                MatchStatus = MatchStatus.Setup
            };
            
            var fixtureStateActor = ActorOfAsTestActorRef<FixtureStateActor>(
                Props.Create(() =>
                    new FixtureStateActor(
                        SettingsMock.Object,
                        StoreProviderMock.Object)),
                FixtureStateActor.ActorName);

            //
            //Act
            //
            var fixtureState =
                fixtureStateActor
                    .Ask<FixtureState>(new GetFixtureStateMsg { FixtureId = storedFixtureState.Id })
                    .Result;
            //initial null state
            Assert.IsNull(fixtureState);
            //upsert the state
            fixtureStateActor.Ask(
                new UpdateFixtureStateMsg
                {
                    FixtureId = storedFixtureState.Id,
                    Sport = storedFixtureState.Sport,
                    Status = storedFixtureState.MatchStatus,
                    Epoch = storedFixtureState.Epoch,
                    Sequence = storedFixtureState.Sequence
                });

            //
            //Assert
            //
            fixtureState =
                fixtureStateActor
                    .Ask<FixtureState>(new GetFixtureStateMsg { FixtureId = storedFixtureState.Id })
                    .Result;
            Assert.AreEqual(storedFixtureState, fixtureState);
        }

        /// <summary>
        /// This test ensures the RemoveFixtureStateMsg removes the fixture state as expected
        /// </summary>
        [Test]
        [Category(FIXTURE_STATE_ACTOR_CATEGORY)]
        public void GivenExistingFixtureStateWhenSendRemoveFixtureStateMsgThenRemovesTheFixtureState()
        {
            //
            //Arrange
            //
            var storedFixtureState = new FixtureState
            {
                Id = "id1",
                Sport = "sport1",
                Epoch = 1,
                Sequence = 1,
                MatchStatus = MatchStatus.Setup
            };
            StoreProviderMock.Setup(o => o.Read(It.IsAny<string>()))
                .Returns(JsonConvert.SerializeObject(
                    new Dictionary<string, FixtureState> { { storedFixtureState.Id, storedFixtureState } },
                    Formatting.Indented));
            var fixtureStateActor = ActorOfAsTestActorRef<FixtureStateActor>(
                Props.Create(() =>
                    new FixtureStateActor(
                        SettingsMock.Object,
                        StoreProviderMock.Object)),
                FixtureStateActor.ActorName);
            var fixtureState =
                fixtureStateActor
                    .Ask<FixtureState>(new GetFixtureStateMsg { FixtureId = storedFixtureState.Id })
                    .Result;
            Assert.AreEqual(storedFixtureState, fixtureState);

            //
            //Act
            //
            //Send RemoveFixtureStateMsg and wait for completion
            fixtureStateActor.Ask(new RemoveFixtureStateMsg { FixtureId = storedFixtureState.Id });

            fixtureState =
                fixtureStateActor
                    .Ask<FixtureState>(new GetFixtureStateMsg { FixtureId = storedFixtureState.Id })
                    .Result;

            //
            //Assert
            //
            Assert.IsNull(fixtureState);
        }

        /// <summary>
        /// This test ensures the fixtures state are correctly persisted by the store provider
        /// </summary>
        [Test]
        [Category(FIXTURE_STATE_ACTOR_CATEGORY)]
        public void GivenFixtureStateActorWhenSpecifiedIntervalPassesThenStateIsPersistedToStore()
        {
            //
            //Arrange
            //
            ActorOfAsTestActorRef<FixtureStateActor>(
                Props.Create(() =>
                    new FixtureStateActor(
                        SettingsMock.Object,
                        StoreProviderMock.Object)),
                FixtureStateActor.ActorName);

            //
            //Act
            //
            //wait for 2.5s while the internal persistence should be operated at least twice, 
            //given the interval has been mocked to 1s
            Task.Delay(TimeSpan.FromMilliseconds(2500)).Wait();

            //
            //Assert
            //
            StoreProviderMock.Verify(a =>
                    a.Write(
                        It.Is<string>(path => path.Equals(SettingsMock.Object.FixturesStateFilePath)),
                        It.IsAny<string>()),
                Times.AtLeast(2));
        }

        /// <summary>
        /// This test ensures that CheckFixtureStateMsg sets ShouldProcessFixture to true when Match is not over in the state
        /// </summary>
        [Test]
        [Category(FIXTURE_STATE_ACTOR_CATEGORY)]
        public void GivenFixtureStateActorWhenMatchIsOverThenShouldProcessFixtureReturnsTrue()
        {
            //
            //Arrange
            //
            var storedFixtureState = new FixtureState
            {
                Id = "id1",
                Sport = "sport1",
                Epoch = 1,
                Sequence = 1,
                MatchStatus = MatchStatus.InRunning
            };
            var fixtureStateActor = ActorOfAsTestActorRef<FixtureStateActor>(
                Props.Create(() =>
                    new FixtureStateActor(
                        SettingsMock.Object,
                        StoreProviderMock.Object)),
                FixtureStateActor.ActorName);

            //
            //Act
            //
            //upsert the state
            fixtureStateActor.Ask(
                new UpdateFixtureStateMsg
                {
                    FixtureId = storedFixtureState.Id,
                    Sport = storedFixtureState.Sport,
                    Status = storedFixtureState.MatchStatus,
                    Epoch = storedFixtureState.Epoch,
                    Sequence = storedFixtureState.Sequence
                });

            //
            //Assert
            //
            var fixtureState =
                fixtureStateActor
                    .Ask<FixtureState>(new GetFixtureStateMsg { FixtureId = storedFixtureState.Id })
                    .Result;
            Assert.AreEqual(storedFixtureState, fixtureState);

            Mock<IResourceFacade> resourceMock = new Mock<IResourceFacade>();
            resourceMock.SetupGet(o => o.Id).Returns(storedFixtureState.Id);
            resourceMock.SetupGet(o => o.MatchStatus).Returns(MatchStatus.MatchOver);

            //
            //Act
            //
            var checkFixtureStateMsg =
                fixtureStateActor
                    .Ask<CheckFixtureStateMsg>(new CheckFixtureStateMsg { Resource = resourceMock.Object })
                    .Result;

            //
            //Assert
            //
            Assert.IsTrue(checkFixtureStateMsg.ShouldProcessFixture);
        }

        /// <summary>
        /// This test ensures that CheckFixtureStateMsg sets ShouldProcessFixture to false when Match is already over in the state
        /// </summary>
        [Test]
        [Category(FIXTURE_STATE_ACTOR_CATEGORY)]
        public void GivenFixtureStateActorWhenMatchIsOverThenShouldProcessFixtureReturnsFalse()
        {
            //
            //Arrange
            //
            var storedFixtureState = new FixtureState
            {
                Id = "id1",
                Sport = "sport1",
                Epoch = 1,
                Sequence = 1,
                MatchStatus = MatchStatus.MatchOver
            };
            var fixtureStateActor = ActorOfAsTestActorRef<FixtureStateActor>(
                Props.Create(() =>
                    new FixtureStateActor(
                        SettingsMock.Object,
                        StoreProviderMock.Object)),
                FixtureStateActor.ActorName);

            //
            //Act
            //
            //upsert the state
            fixtureStateActor.Ask(
                new UpdateFixtureStateMsg
                {
                    FixtureId = storedFixtureState.Id,
                    Sport = storedFixtureState.Sport,
                    Status = storedFixtureState.MatchStatus,
                    Epoch = storedFixtureState.Epoch,
                    Sequence = storedFixtureState.Sequence
                });

            //
            //Assert
            //
            var fixtureState =
                fixtureStateActor
                    .Ask<FixtureState>(new GetFixtureStateMsg { FixtureId = storedFixtureState.Id })
                    .Result;
            Assert.AreEqual(storedFixtureState, fixtureState);

            Mock<IResourceFacade> resourceMock = new Mock<IResourceFacade>();
            resourceMock.SetupGet(o => o.Id).Returns(storedFixtureState.Id);
            resourceMock.SetupGet(o => o.MatchStatus).Returns(MatchStatus.MatchOver);

            //
            //Act
            //
            var checkFixtureStateMsg =
                fixtureStateActor
                    .Ask<CheckFixtureStateMsg>(new CheckFixtureStateMsg { Resource = resourceMock.Object })
                    .Result;

            //
            //Assert
            //
            Assert.IsFalse(checkFixtureStateMsg.ShouldProcessFixture);
        }

        #endregion
    }
}

//Copyright 2014 Spin Services Limited

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
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Events;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Extensions;

namespace SS.Integration.Adapter.Diagnostics.Testing
{
    [TestFixture]
    public class SupervisorTest
    {
        private static Mock<ISettings> _settings;
        private static Mock<IResourceFacade> _resource;
        private static Mock<IAdapterPlugin> _connector;
        private static Supervisor _supervisor;
        private static StateManager _provider;

        [SetUp]
        public static void SetUpMocks()
        {
            _settings = new Mock<ISettings>();
            _settings.Setup(x => x.MarketFiltersDirectory).Returns(".");
            _settings.Setup(x => x.EventStateFilePath).Returns(".");

            _provider = new StateManager(_settings.Object);

            _resource = new Mock<IResourceFacade>();
            _resource.Setup(r => r.Sport).Returns("FantasyFootball");
            _resource.Setup(r => r.StartStreaming()).Raises(r => r.StreamConnected += null, new EventArgs());

            _connector = new Mock<IAdapterPlugin>();
            
            var stateManager = new StateManager(new Mock<ISettings>().Object);

            _supervisor = new Supervisor(_settings.Object);
            _supervisor.StateManager = stateManager;

            var supervisorService = new Mock<ISupervisorService>();
            supervisorService.Setup(x => x.StreamingService).Returns(new Mock<ISupervisorStreamingService>().Object);


            _supervisor.Service = supervisorService.Object;
            _supervisor.Proxy = new Mock<ISupervisorProxy>().Object;
            
            var plugin = new Mock<IAdapterPlugin>();
            
            new SuspensionManager(stateManager, plugin.Object);

        }

        //This test should verify that when you force snapshot there is no filtering(market rules) applied
        [Test]
        public void ForceSnapshotTest()
        {
            var snapshotId = "testFixtureId";
            Fixture fixture = GetSnapshotWithMarkets(snapshotId);

            _resource.Setup(x => x.Id).Returns(snapshotId);
            _resource.Setup(x => x.Content).Returns(new Summary());
            _resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            _resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

            _supervisor.CreateStreamListener(_resource.Object, _connector.Object);

            _supervisor.ForceSnapshot(fixture.Id);
            _supervisor.ForceSnapshot(fixture.Id);

            

            //inital snapshot + 2 forced snapshots = 3
            _resource.Verify(x => x.GetSnapshot(), Times.Exactly(3));
            _connector.Verify(x => x.ProcessSnapshot(It.Is<Fixture>(f => f.Markets.Count == 1), It.IsAny<bool>()), Times.Exactly(3));
        }

        [Test]
        public void GetFullOverviewTest()
        {
            var fixtureOneId = "fixtureOne";
            var fixtureTwoId = "fixtureTwo";

            var resourceOne = new Mock<IResourceFacade>();
            var resourceTwo = new Mock<IResourceFacade>();

            resourceOne.Setup(x => x.Id).Returns(fixtureOneId);
            resourceOne.Setup(x => x.Content).Returns(new Summary());
            resourceOne.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resourceOne.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureOneId)));
            resourceOne.Setup(x => x.Sport).Returns("TestSport1");

            resourceTwo.Setup(x => x.Id).Returns(fixtureTwoId);
            resourceTwo.Setup(x => x.Content).Returns(new Summary());
            resourceTwo.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resourceTwo.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureTwoId)));
            resourceTwo.Setup(x => x.Sport).Returns("TestSport2");

            _supervisor.CreateStreamListener(resourceOne.Object, _connector.Object);
            _supervisor.CreateStreamListener(resourceTwo.Object, _connector.Object);

            var fixtureOverviews = _supervisor.GetFixtures();
            fixtureOverviews.Should().NotBeNullOrEmpty();
            fixtureOverviews.Should().Contain(f => f.Id == fixtureOneId);
            fixtureOverviews.Should().Contain(f => f.Id == fixtureTwoId);
            fixtureOverviews.Any(f=> f.Sport == "TestSport1").Should().BeTrue();
            fixtureOverviews.Any(f => f.Sport == "TestSport2").Should().BeTrue();
        }

        [Test]
        public void GetSportsTest()
        {
            var fixtureOneId = "fixtureOne";
            var fixtureTwoId = "fixtureTwo";

            var resourceOne = new Mock<IResourceFacade>();
            var resourceTwo = new Mock<IResourceFacade>();

            resourceOne.Setup(x => x.Id).Returns(fixtureOneId);
            resourceOne.Setup(x => x.Content).Returns(new Summary());
            resourceOne.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resourceOne.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureOneId)));
            resourceOne.Setup(x => x.Sport).Returns("TestSport1");
            resourceOne.Setup(x => x.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);


            resourceTwo.Setup(x => x.Id).Returns(fixtureTwoId);
            resourceTwo.Setup(x => x.Content).Returns(new Summary());
            resourceTwo.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resourceTwo.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureTwoId)));
            resourceTwo.Setup(x => x.Sport).Returns("TestSport2");
            resourceTwo.Setup(x => x.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            _supervisor.CreateStreamListener(resourceOne.Object, _connector.Object);
            _supervisor.CreateStreamListener(resourceTwo.Object, _connector.Object);
            
            _supervisor.GetSports().Should().NotBeEmpty();
            _supervisor.GetSports().Count().Should().Be(2);
            _supervisor.GetSportOverview("TestSport1").Should().NotBeNull();
            _supervisor.GetSportOverview("TestSport1").InPlay.Should().Be(1);
        }
        

        [Test]
        public void GetDeltaOverviewTest()
        {
            var fixtureOneId = "fixtureOne";
            
            _resource.Setup(x => x.Id).Returns(fixtureOneId);
            _resource.Setup(x => x.Content).Returns(new Summary());
            _resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            _resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureOneId)));
            _resource.Setup(x => x.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            var deltas = new List<IFixtureOverviewDelta>();
            var subscriber = _supervisor.GetFixtureOverviewStream().ObserveOn(NewThreadScheduler.Default).Subscribe(deltas.Add);

            _supervisor.CreateStreamListener(_resource.Object, _connector.Object);

            var fixtureOverviews = _supervisor.GetFixtures();
            fixtureOverviews.Should().NotBeNullOrEmpty();
            fixtureOverviews.Should().Contain(f => f.Id == fixtureOneId);
            
            _supervisor.StartStreaming(fixtureOneId);

            var epoch = 1;

            var streamUpdate = new Fixture
            {
                Id = fixtureOneId,
                Sequence = 2,
                Epoch = epoch,
                MatchStatus = ((int)MatchStatus.InRunning).ToString()
            };

            SendStreamUpdate(streamUpdate);
            
            Thread.Sleep(1000);

            deltas.Should().NotBeEmpty();
            var sequenceTwoDeltas = deltas.Where(d => d.FeedUpdate != null && d.FeedUpdate.Sequence == 2).ToList();
            sequenceTwoDeltas.Should().NotBeNull();
            sequenceTwoDeltas.Count.Should().Be(2);
            
            //FeedUpdate with IsProcessed in both states exists
            sequenceTwoDeltas.Any(d => d.FeedUpdate.IsProcessed).Should().BeTrue();
            sequenceTwoDeltas.Any(d => !d.FeedUpdate.IsProcessed).Should().BeTrue();

            //deltas are filtered by sequence = 2 and there was only stream update with that sequence
            sequenceTwoDeltas.All(d => !d.FeedUpdate.IsSnapshot).Should().BeTrue();
            sequenceTwoDeltas.All(d => d.FeedUpdate.Epoch == epoch).Should().BeTrue();
            
            subscriber.Dispose();
        }

        [Test]
        public void GetDeltaErrorsTest()
        {
            var fixtureOneId = "fixtureOne";

            var resourceOne = new Mock<IResourceFacade>();

            resourceOne.Setup(x => x.Id).Returns(fixtureOneId);
            resourceOne.Setup(x => x.Content).Returns(new Summary());
            resourceOne.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resourceOne.SetupSequence(x => x.GetSnapshot())
                .Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureOneId)))
                .Returns(String.Empty)
                .Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureOneId, 10, 3)));

            resourceOne.Setup(x => x.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            _supervisor.CreateStreamListener(resourceOne.Object, _connector.Object);

            var fixtureOverviews = _supervisor.GetFixtures();
            fixtureOverviews.Should().NotBeNullOrEmpty();
            fixtureOverviews.Should().Contain(f => f.Id == fixtureOneId);

            _supervisor.StartStreaming(fixtureOneId);

            var streamUpdate = new Fixture
            {
                Id = fixtureOneId,
                Sequence = 2,
                //Epoch increased
                Epoch = 10,
                MatchStatus = ((int)MatchStatus.InRunning).ToString()
            };

            var deltas = new List<IFixtureOverviewDelta>();

            using (var subscriber = _supervisor.GetFixtureOverviewStream().Subscribe(deltas.Add))
            {
                //in order to generate error the resource is setup to return empty snapshot
                //the snapshot should be taken because epoch is changed
                SendStreamUpdate(streamUpdate);

                deltas.Should().NotBeEmpty();
                deltas.FirstOrDefault(d => d.LastError != null).Should().NotBeNull();

                //error was resolved with a further snapshot
                deltas.FirstOrDefault(d => d.LastError != null && !d.LastError.IsErrored).Should().NotBeNull();
            }
        }

        [Test]
        public void CheckUpdatesAreTrackedTest()
        {
            var fixtureOneId = "fixtureOne";

            var resourceOne = new Mock<IResourceFacade>();

            resourceOne.Setup(x => x.Id).Returns(fixtureOneId);
            resourceOne.Setup(x => x.Content).Returns(new Summary());
            resourceOne.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resourceOne.SetupSequence(x => x.GetSnapshot())
                .Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureOneId)))
                .Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureOneId, 10, 3)));

            resourceOne.Setup(x => x.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            _supervisor.CreateStreamListener(resourceOne.Object, _connector.Object);

            var fixtureOverviews = _supervisor.GetFixtures();
            fixtureOverviews.Should().NotBeNullOrEmpty();
            fixtureOverviews.Should().Contain(f => f.Id == fixtureOneId);

            _supervisor.StartStreaming(fixtureOneId);

            var streamUpdate = new Fixture
            {
                Id = fixtureOneId,
                Sequence = 2,
                //Epoch increased
                Epoch = 10,
                MatchStatus = ((int)MatchStatus.InRunning).ToString()
            };


            Enumerable.Range(3, 13).ForEach(s =>
            {
                streamUpdate.Sequence = s;
                SendStreamUpdate(streamUpdate);
            });

            var fixtureOverview = _supervisor.GetFixtureOverview(fixtureOneId);
            fixtureOverview.GetFeedAudit().Should().NotBeEmpty();
            fixtureOverview.GetFeedAudit().FirstOrDefault(f => f.Sequence == 12 && f.IsProcessed).Should().NotBeNull();

            fixtureOverview.FeedUpdate.Sequence.Should().BeGreaterThan(10);
        }

        [Test]
        public void CheckErrorsAreTrackedTest()
        {
            var fixtureOneId = "fixtureOne";
            
            _resource.Setup(x => x.Id).Returns(fixtureOneId);
            _resource.Setup(x => x.Content).Returns(new Summary());
            _resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            _resource.SetupSequence(x => x.GetSnapshot())
                .Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureOneId)))
                .Returns(String.Empty)
                .Returns(String.Empty)
                .Returns(String.Empty)
                .Returns(String.Empty)
                .Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureOneId, 10, 15)));

            _resource.Setup(x => x.StartStreaming()).Raises(r => r.StreamConnected += null, EventArgs.Empty);

            _supervisor.CreateStreamListener(_resource.Object, _connector.Object);

            var fixtureOverviews = _supervisor.GetFixtures();
            fixtureOverviews.Should().NotBeNullOrEmpty();
            fixtureOverviews.Should().Contain(f => f.Id == fixtureOneId);

            _supervisor.StartStreaming(fixtureOneId);

            var streamUpdate = new Fixture
            {
                Id = fixtureOneId,
                Sequence = 2,
                //Epoch increased
                Epoch = 10,
                MatchStatus = ((int)MatchStatus.InRunning).ToString()
            };

            var deltas = new List<IFixtureOverviewDelta>();

            using (var subscriber = _supervisor.GetFixtureOverviewStream().Subscribe(deltas.Add))
            {
                Enumerable.Range(3, 10).ForEach(s =>
                {
                    streamUpdate.Sequence = s;
                    SendStreamUpdate(streamUpdate);
                });
            }

            var fixtureOverview = _supervisor.GetFixtureOverview(fixtureOneId);
            fixtureOverview.GetErrorsAudit().Should().NotBeEmpty();
            var errorsAudit = fixtureOverview.GetErrorsAudit();

            //at least 4 failed snapshots
            errorsAudit.Count().Should().Be(4);

            //the final snapshot sholud have succeeded
            fixtureOverview.LastError.IsErrored.Should().BeFalse();

            //there should be delta notification with LastError update IsErrored = false
            deltas.FirstOrDefault(d=> 
                d.LastError != null 
                && d.LastError.ResolvedAt == fixtureOverview.LastError.ResolvedAt 
                && d.LastError.Sequence == fixtureOverview.LastError.Sequence).Should().NotBeNull();
        }

        private void SendStreamUpdate(Fixture streamUpdate)
        {
            var listener = _supervisor.GetType()
                .InvokeMember("GetStreamListener"
                    , BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                    null, _supervisor, new object[] { streamUpdate.Id }) as StreamListener;

            var message = new StreamMessage { Content = streamUpdate };

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(JsonConvert.SerializeObject(message)));
        }

        private Fixture GetSnapshotWithMarkets(string id, int epoch = 1, int sequence = 1)
        {
            var snapshot = new Fixture 
            { Id = id, Sequence = sequence, MatchStatus = ((int)MatchStatus.InRunning).ToString(), Epoch = epoch };

            AddMarket(snapshot);
            AddTags(snapshot);

            return snapshot;
        }

        private void AddTags(Fixture snapshot)
        {
            snapshot.Tags.Add("SSLNCompetitionId","TestCompId");
            snapshot.Tags.Add("SSLNCompetitionName", "Test Competition Name");
        }

        private void AddMarket(Fixture snapshot)
        {
            var selections = new List<Selection>
            {
                new Selection {Id = "Sel1", Status = "1", Price = 0.45, Name = "Sel 1", Tradable = false},
                new Selection {Id = "Sel2", Status = "1", Price = 0.45, Name = "Sel 2", Tradable = false},
                new Selection {Id = "Sel3", Status = "1", Price = 0.45, Name = "Sel 3", Tradable = false}
            };

            var market = new Market("testMarketId");
            market.Selections.AddRange(selections);

            snapshot.Markets.Add(market);
        }
    }
}


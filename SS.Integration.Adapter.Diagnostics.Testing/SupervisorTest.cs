using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Events;
using SS.Integration.Adapter.Diagnostic;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;

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
            _connector = new Mock<IAdapterPlugin>();

            _supervisor = new Supervisor(_settings.Object);
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
            
            _supervisor.CreateStreamListener(_resource.Object,_provider,_connector.Object);
            
            _supervisor.ForceSnapshot(fixture.Id);
            _supervisor.ForceSnapshot(fixture.Id);

            _resource.Verify(x=> x.GetSnapshot(),Times.Exactly(2));
            _connector.Verify(x=> x.ProcessSnapshot(It.Is<Fixture>(f=> f.Markets.Count == 1),It.IsAny<bool>()),Times.Exactly(2));
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

            resourceTwo.Setup(x => x.Id).Returns(fixtureTwoId);
            resourceTwo.Setup(x => x.Content).Returns(new Summary());
            resourceTwo.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resourceTwo.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureTwoId)));
            

            _supervisor.CreateStreamListener(resourceOne.Object,_provider,_connector.Object);
            _supervisor.CreateStreamListener(resourceTwo.Object, _provider, _connector.Object);

            var fixtureOverviews = _supervisor.GetFixtures();
            fixtureOverviews.Should().NotBeNullOrEmpty();
            fixtureOverviews.Should().Contain(f => f.Id == fixtureOneId);
            fixtureOverviews.Should().Contain(f => f.Id == fixtureTwoId);
        }

        [Test]
        public void GetDeltaOverviewTest()
        {
            var fixtureOneId = "fixtureOne";
            var fixtureTwoId = "fixtureTwo";
            
            var resourceOne = new Mock<IResourceFacade>();
            var resourceTwo = new Mock<IResourceFacade>();

            resourceOne.Setup(x => x.Id).Returns(fixtureOneId);
            resourceOne.Setup(x => x.Content).Returns(new Summary());
            resourceOne.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resourceOne.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureOneId)));
            resourceOne.Setup(x => x.StartStreaming()).Raises(r => r.StreamConnected += null,EventArgs.Empty);

            resourceTwo.Setup(x => x.Id).Returns(fixtureTwoId);
            resourceTwo.Setup(x => x.Content).Returns(new Summary());
            resourceTwo.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
            resourceTwo.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(GetSnapshotWithMarkets(fixtureTwoId)));


            _supervisor.CreateStreamListener(resourceOne.Object, _provider, _connector.Object);
            _supervisor.CreateStreamListener(resourceTwo.Object, _provider, _connector.Object);

            var fixtureOverviews = _supervisor.GetFixtures();
            fixtureOverviews.Should().NotBeNullOrEmpty();
            fixtureOverviews.Should().Contain(f => f.Id == fixtureOneId);
            fixtureOverviews.Should().Contain(f => f.Id == fixtureTwoId);

            var delta = new List<IFixtureOverviewDelta>();

            var subscriber = _supervisor.GetFixtureOverviewStream().Subscribe(delta.Add);

            _supervisor.StartStreaming(fixtureOneId);


            var streamUpdate = new Fixture
            {
                Id = fixtureOneId,
                Sequence = 2,
                Epoch = 1
            };

            SendStreamUpdate(streamUpdate);

            delta.Should().NotBeEmpty();

            //subscriber.Dispose();
        }

        private void SendStreamUpdate(Fixture streamUpdate)
        {
            var listener = _supervisor.GetType()
                .InvokeMember("GetStreamListener"
                    ,BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                    null, _supervisor, new object[] { streamUpdate.Id}) as StreamListener;

            var message = new StreamMessage { Content = streamUpdate };

            listener.ResourceOnStreamEvent(null, new StreamEventArgs(JsonConvert.SerializeObject(message)));
        }

        private Fixture GetSnapshotWithMarkets(string id)
        {
            var snapshot = new Fixture { Id = id, Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString(), Epoch = 1};

            AddMarket(snapshot);

            return snapshot;
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

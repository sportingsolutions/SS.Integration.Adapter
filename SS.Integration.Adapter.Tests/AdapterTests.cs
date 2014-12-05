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

using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using System.Threading;
using Moq;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class AdapterTests
    {
        private Mock<IStateManager> _state;

        [SetUp]
        public void TestSetup()
        {
            _state = new Mock<IStateManager>();
        }
        
        [Category("Adapter")]
        [Test]
        public void ShouldStartAndStopNoSports()
        {
            var settings = new Mock<ISettings>();
            settings.Setup(x => x.EventStateFilePath).Returns(".");



            var streamListenerManager = new StreamListenerManager(settings.Object,_state.Object);
            var service = new Mock<IServiceFacade>();
            service.Setup(x => x.IsConnected).Returns(true);
            var connector = new Mock<IAdapterPlugin>();
            var listener = new Mock<IListener>();

            settings.Setup(s => s.FixtureCheckerFrequency).Returns(10000);
            service.Setup(s => s.Connect());

            var adapter = new Adapter(
                settings.Object,
                service.Object,
                connector.Object,
                streamListenerManager);

            adapter.Start();
            adapter.Stop();

            service.VerifyAll();
            listener.Verify(l => l.Start(), Times.Never());
            listener.Verify(l => l.Stop(), Times.Never());
        }

        [Category("Adapter")]
        [Test]
        public void AdapterGetVersionTest()
        {
            var settings = new Mock<ISettings>();
            settings.Setup(x => x.EventStateFilePath).Returns(".");
            
            var streamListenerManager = new StreamListenerManager(settings.Object, _state.Object);
            var service = new Mock<IServiceFacade>();
            service.Setup(x => x.IsConnected).Returns(true);
            var connector = new Mock<IAdapterPlugin>();
            var listener = new Mock<IListener>();

            settings.Setup(s => s.FixtureCheckerFrequency).Returns(10000);
            service.Setup(s => s.Connect());

            var adapter = new Adapter(
                settings.Object,
                service.Object,
                connector.Object,
                streamListenerManager);

            adapter.Start();
            adapter.Stop();

            var adapterVersionInfo = new AdapterVersionInfo();
            adapterVersionInfo.AdapterVersion.Should().NotBeNullOrEmpty();
            adapterVersionInfo.UdapiSDKVersion.Should().NotBeNullOrEmpty();
        }

        [Category("Adapter")]
        [Test]
        public void ShouldStartAndStopWithFewSportsNoFixtures()
        {
            var streamListenerManager = new Mock<IStreamListenerManager>();
            var settings = new Mock<ISettings>();
            settings.Setup(x => x.EventStateFilePath).Returns(".");
            var service = new Mock<IServiceFacade>();
            service.Setup(x => x.IsConnected).Returns(true);
            var connector = new Mock<IAdapterPlugin>();
            var listener = new Mock<IListener>();

            settings.Setup(s => s.FixtureCheckerFrequency).Returns(10000);
            service.Setup(s => s.Connect());

            var adapter = new Adapter(
                settings.Object,
                service.Object,
                connector.Object,
                streamListenerManager.Object
                );

            foreach (var sport in this.Sports(ListOfSports.GiveMeFew))
                adapter.AddSport(sport);

            adapter.Start();
            adapter.Stop();

            service.VerifyAll();
            listener.Verify(l => l.Start(), Times.Never());
            listener.Verify(l => l.Stop(), Times.Never());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Never());
        }

        [Test]
        [Category("Adapter")]
        public void ShouldCreateListeners()
        {
            var streamListenerManager = new Mock<IStreamListenerManager>();
            var settings = new Mock<ISettings>();
            settings.Setup(x => x.EventStateFilePath).Returns(".");
            var service = new Mock<IServiceFacade>();
            service.Setup(x => x.IsConnected).Returns(true);
            var connector = new Mock<IAdapterPlugin>();
            var eventState = new Mock<IEventState>();

            var fixtureOne = new Mock<IResourceFacade>();
            var fixtureTwo = new Mock<IResourceFacade>();

            fixtureOne.Setup(f => f.Id).Returns("1");
            fixtureOne.Setup(f => f.GetSnapshot()).Returns(TestHelper.GetSnapshotJson());
            fixtureOne.Setup(f => f.Content).Returns(new Summary { Id = "1", Date = "23/05/2012", StartTime = "10:20", MatchStatus = 1});

            fixtureTwo.Setup(f => f.Id).Returns("2");
            fixtureTwo.Setup(f => f.GetSnapshot()).Returns(TestHelper.GetSnapshotJson());
            fixtureTwo.Setup(f => f.Content)
                      .Returns(new Summary
                          {
                              Id = "2", Date = "23/05/2012", StartTime = "13:20", MatchStatus = 1
                          });

            service.Setup(s => s.GetResources("Football")).Returns(new List<IResourceFacade> { fixtureOne.Object, fixtureTwo.Object });

            settings.Setup(s => s.FixtureCreationConcurrency).Returns(2);

            var adapter = new Adapter(
                settings.Object,
                service.Object,
                connector.Object,
                streamListenerManager.Object
                );

            adapter.Start();
            
            adapter.ProcessSport("Football");
            adapter.ProcessSport("Rugby");

            Thread.Yield();

            service.VerifyAll();
            
            eventState.Verify(es => es.RemoveFixture(It.IsAny<string>()), Times.Never());
        }

        /// <summary>
        /// Here I want to test that when a resource
        /// is being processed by the adapter thread
        /// (timer tick) then it must not be processed
        /// by another thread at the same time
        /// </summary>
        [Test]
        [Category("Adapter")]
        public void ResourceIsProcessedExclusivelyTest()
        {
            var settings = new Mock<ISettings>();
            var service = new Mock<IServiceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var state = new Mock<IEventState>();
            
            settings.Setup(x => x.EventStateFilePath).Returns(".");
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");
            settings.Setup(x => x.FixtureCreationConcurrency).Returns(1);
            settings.Setup(x => x.FixtureCheckerFrequency).Returns(500);

            var streamListenerManager = new StreamListenerManager(settings.Object,_state.Object);
            var sport = new Mock<IFeature>();

            var resource = new Mock<IResourceFacade>();
            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary { Sequence = 1 });

            service.Setup(x => x.GetSports()).Returns(new List<IFeature> { sport.Object });
            service.Setup(x => x.GetResources(It.IsAny<string>())).Returns(new List<IResourceFacade> { resource.Object });
            service.Setup(x => x.IsConnected).Returns(true);



            Adapter adapter = new Adapter(settings.Object, service.Object, connector.Object, streamListenerManager);
            streamListenerManager.StreamCreated += adapter_StreamCreated;
            streamListenerManager.EventState = state.Object;

            // after this call a stream listener for the above resource will be created
            // and the thread that created it will be blocked on the adapter_StreamCreated 
            // event handler until we un-block it
            adapter.Start();

            // As the FixtureCheckerFrequency is half second, before returning
            // from the event handler, the adapter's timer tick surely has
            // been fired several times. As we haven't yet returned from
            // the event handler, for the adapter the resource is still
            // being processed. Here we want to check that this is true.

            // For checking this we check how many times the adapter
            // interrogates EventState.GetFixtureState("ABC")
            // (that call is made immediately before adding the resource
            // to the creation queue - if that detail change, we need
            // to revisit this unit test

            Thread.Sleep(5000);

            lock (streamListenerManager)
            {
                Monitor.PulseAll(streamListenerManager);
                state.Verify(x => x.GetFixtureState("ABC"), Times.AtLeastOnce());
            }

            adapter.Stop();

        }

        private void adapter_StreamCreated(object sender, string fixtureId)
        {
            lock (sender)
            {
                Monitor.Wait(sender);
            }
        }

        [Test]
        [Category("Adapter")]
        public void AdapterIsDisposedCorrectlyTest()
        {
            var streamListenerManager = new Mock<IStreamListenerManager>();
            var settings = new Mock<ISettings>();
            var service = new Mock<IServiceFacade>();
            var connector = new Mock<IAdapterPlugin>();

            settings.Setup(x => x.EventStateFilePath).Returns(".");
            settings.Setup(x => x.MarketFiltersDirectory).Returns(".");
            settings.Setup(x => x.FixtureCreationConcurrency).Returns(1);
            settings.Setup(x => x.FixtureCheckerFrequency).Returns(500);

            var sport = new Mock<IFeature>();

            var resource = new Mock<IResourceFacade>();
            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary { Sequence = 1 });

            service.Setup(x => x.GetSports()).Returns(new List<IFeature> { sport.Object });
            service.Setup(x => x.GetResources(It.IsAny<string>())).Returns(new List<IResourceFacade> { resource.Object });
            service.Setup(x => x.IsConnected).Returns(true);


            Adapter adapter = new Adapter(settings.Object, service.Object, connector.Object,new StreamListenerManager(settings.Object,_state.Object));
            streamListenerManager.Object.StreamCreated += adapter_StreamCreated;

            adapter.Start();

            Thread.Sleep(1000);

            bool error = false;

            // as stop needs to wait for thread termination, and all the threads
            // are waiting on a lock, we need to call this on a separate thread
            Task t = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        adapter.Stop();
                    }
                    catch
                    {
                        error = true;
                    }
                }
                
            );

            Thread.Sleep(2000);

            lock (adapter)
            {
                Monitor.PulseAll(adapter);
            }

            t.Wait();
            error.Should().BeFalse();
        }

        /// <summary>
        /// I want to make sure that the adapter only
        /// disposes a StreamListener object if the fixture
        /// doesn't appear on Connect for
        /// "LISTENER_DISPOSING_SAFE_GUARD" times
        /// </summary>
        [Test]
        [Category("Adapter")]
        public void DisposeStreamListenerSafeGuardTest()
        {
            var settings = new Mock<ISettings>();
            settings.Setup(x => x.EventStateFilePath).Returns(".");
            settings.Setup(x => x.FixtureCreationConcurrency).Returns(2);
            settings.Setup(x => x.FixtureCheckerFrequency).Returns(1200000); 

            var service = new Mock<IServiceFacade>();
            var streamListenerManager = new StreamListenerManager(settings.Object,_state.Object);
            var connector = new Mock<IAdapterPlugin>();
            var fixtureOne = new Mock<IResourceFacade>();
            var fixtureTwo = new Mock<IResourceFacade>();

            int created = 0;            

            fixtureOne.Setup(f => f.Id).Returns("1");
            fixtureOne.Setup(f => f.Sport).Returns("Football");
            fixtureOne.Setup(f => f.GetSnapshot()).Returns(TestHelper.GetSnapshotJson());
            fixtureOne.Setup(f => f.Content).Returns(new Summary { Id = "1", Date = "23/05/2012", StartTime = "10:20", MatchStatus = (int)MatchStatus.Setup });

            fixtureTwo.Setup(f => f.Id).Returns("2");
            fixtureTwo.Setup(f => f.Sport).Returns("Football");
            fixtureTwo.Setup(f => f.GetSnapshot()).Returns(string.Empty);
            fixtureTwo.Setup(f => f.Content).Returns(new Summary { Id = "2", Date = "23/05/2012", StartTime = "10:30", MatchStatus = (int)MatchStatus.Setup });

            service.Setup(x => x.GetResources("Football")).Returns(() => new List<IResourceFacade> { fixtureOne.Object, fixtureTwo.Object });
            service.Setup(x => x.IsConnected).Returns(false);

            Adapter adapter = new Adapter(settings.Object, service.Object, connector.Object,streamListenerManager);
            adapter.AddSport("Football");

            streamListenerManager.StreamCreated += delegate(object sender, string e)
                {
                    created++;
                };

            streamListenerManager.StreamRemoved += delegate(object sender, string e)
                {
                    created--;
                };

            adapter.Start();

            adapter.TimerEvent();

            Thread.Sleep(500);

            created.Should().Be(2);

            service.Setup(s => s.GetResources("Football")).Returns(() => 
            {
                return new List<IResourceFacade> {fixtureTwo.Object}; 
            });

            adapter.TimerEvent();

            Thread.Sleep(500);
            created.Should().Be(2);

            adapter.TimerEvent();
            Thread.Sleep(500);

            created.Should().Be(1);
            
        }

        private IEnumerable<string> Sports(ListOfSports howMany)
        {
            if (howMany == ListOfSports.None)
            {
                yield break;
            }
                
            yield return "Football";

            if (howMany == ListOfSports.GiveMeFew)
            {
                yield return "Tennis";
                yield return "Rugby";
            }
        }

        private enum ListOfSports
        {
            None,
            GiveMeFew
        }
    }
}

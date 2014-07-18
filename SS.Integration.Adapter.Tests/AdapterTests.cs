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
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class AdapterTests
    {
        [Category("Adapter")]
        [Test]
        public void ShouldStartAndStopNoSports()
        {
            var settings = new Mock<ISettings>();
            settings.Setup(x => x.EventStateFilePath).Returns(".");
            var service = new Mock<IServiceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var listener = new Mock<IListener>();

            settings.Setup(s => s.FixtureCheckerFrequency).Returns(10000);
            settings.Setup(s => s.SuspendAllMarketsOnShutdown).Returns(false);
            service.Setup(s => s.Connect());

            var adapter = new Adapter(
                settings.Object,
                service.Object,
                connector.Object);

            adapter.Start();
            adapter.Stop();

            service.VerifyAll();
            listener.Verify(l => l.Start(), Times.Never());
            listener.Verify(l => l.Stop(), Times.Never());
        }

        [Category("Adapter")]
        [Test]
        public void ShouldStartAndStopWithFewSportsNoFixtures()
        {
            var settings = new Mock<ISettings>();
            settings.Setup(x => x.EventStateFilePath).Returns(".");
            var service = new Mock<IServiceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var listener = new Mock<IListener>();

            settings.Setup(s => s.FixtureCheckerFrequency).Returns(10000);
            settings.Setup(s => s.SuspendAllMarketsOnShutdown).Returns(true);
            service.Setup(s => s.Connect());

            var adapter = new Adapter(
                settings.Object,
                service.Object,
                connector.Object);

            foreach (var sport in this.Sports(ListOfSports.GiveMeFew))
                adapter.AddSport(sport);

            adapter.Start();
            adapter.Stop();

            service.VerifyAll();
            listener.Verify(l => l.Start(), Times.Never());
            listener.Verify(l => l.Stop(), Times.Never());
            connector.Verify(c => c.Suspend(It.IsAny<string>()), Times.Never());
        }

        /*[Category("Adapter")]
        [Ignore]
        public void ShouldStartAndStopWithFewSportsAndFixtures()
        {
            var settings = new Mock<ISettings>();
            settings.Setup(x => x.EventStateFilePath).Returns(".");
            var service = new Mock<IServiceFacade>();
            var connector = new Mock<IAdapterPlugin>();
            var listener = new Mock<IListener>();
            var eventState = new Mock<IEventState>();
            //*********************
            //a semaphore is included to ensure that every 3 threads
            //are finished. this test works but once every ~5/6 tries it fails with a weird 
            //"IndexOutOfRangeException". I actually find out that there's a problem in Moq on multi-threading tests
            //(see https://code.google.com/p/moq/issues/detail?id=249 and https://github.com/Moq/moq4/pull/3.).
            //Update 06/06/2013: the issue related to the link above is solved, but the Moq team can't merge the fix
            //because it causes another issue (see https://github.com/Moq/moq4/issues/47).
            //basically we still need news from the Moq team
            //*********************
            var semaphore = new SemaphoreSlim(3,3);

            listener.Setup(x => x.SuspendMarkets(true)).Callback(() => semaphore.Release(1));

            settings.Setup(s => s.FixtureCheckerFrequency).Returns(10000);
            settings.Setup(s => s.SuspendAllMarketsOnShutdown).Returns(true);
            service.Setup(s => s.Connect());

            var footballFixtureOne = new Mock<IResourceFacade>();
            var footballfixtureTwo = new Mock<IResourceFacade>();
            var tennisFixtureOne = new Mock<IResourceFacade>();

            footballFixtureOne.Setup(f => f.Id).Returns("1");
            footballFixtureOne.Setup(f => f.GetSnapshot()).Returns(TestHelper.GetRawStreamMessage());
            footballfixtureTwo.Setup(f => f.Id).Returns("2");
            footballfixtureTwo.Setup(f => f.GetSnapshot()).Returns(TestHelper.GetRawStreamMessage());
            tennisFixtureOne.Setup(f => f.Id).Returns("3");
            tennisFixtureOne.Setup(f => f.GetSnapshot()).Returns(TestHelper.GetRawStreamMessage());

            service.Setup(s => s.GetResources("Football"))
                   .Returns(new List<IResourceFacade> { footballFixtureOne.Object, footballfixtureTwo.Object });
            service.Setup(s => s.GetResources("Tennis")).Returns(new List<IResourceFacade> { tennisFixtureOne.Object });

            var adapter = new Adapter(
                settings.Object,
                service.Object,
                connector.Object);


            foreach (var sport in this.Sports(ListOfSports.GiveMeFew))
                adapter.AddSport(sport);

            adapter.Start();

            Thread.Sleep(1000);
            
            adapter.Stop();

            semaphore.Wait();
            semaphore.Wait();
            semaphore.Wait();

            Thread.Sleep(2500);

            service.VerifyAll();
            listener.Verify(l => l.Start(), Times.Exactly(3));
            listener.Verify(l => l.Stop(), Times.Exactly(3));
            listener.Verify(l => l.SuspendMarkets(true), Times.Exactly(3));
            eventState.Verify(es => es.RemoveInactiveFixtures("Football", It.IsAny<List<IResourceFacade>>()), Times.Once());
            eventState.Verify(es => es.RemoveInactiveFixtures("Tennis", It.IsAny<List<IResourceFacade>>()), Times.Once());
            eventState.Verify(es => es.RemoveInactiveFixtures("Rugby", It.IsAny<List<IResourceFacade>>()), Times.Never());
        }*/

        [Test]
        [Category("Adapter")]
        public void ShouldCreateListeners()
        {
            var settings = new Mock<ISettings>();
            settings.Setup(x => x.EventStateFilePath).Returns(".");
            var service = new Mock<IServiceFacade>();
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
                connector.Object);

            adapter.Start();
            
            adapter.ProcessSport("Football");
            adapter.ProcessSport("Rugby");

            Thread.Yield();

            service.VerifyAll();
            
            eventState.Verify(es => es.RemoveFixture(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
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

            var sport = new Mock<IFeature>();

            var resource = new Mock<IResourceFacade>();
            resource.Setup(x => x.Id).Returns("ABC");
            resource.Setup(x => x.Content).Returns(new Summary { Sequence = 1 });

            service.Setup(x => x.GetSports()).Returns(new List<IFeature> { sport.Object });
            service.Setup(x => x.GetResources(It.IsAny<string>())).Returns(new List<IResourceFacade> { resource.Object });
            service.Setup(x => x.IsConnected).Returns(true);


            Adapter adapter = new Adapter(settings.Object, service.Object, connector.Object);
            adapter.StreamCreated += adapter_StreamCreated;
            adapter.EventState = state.Object;

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

            lock (adapter)
            {
                Monitor.PulseAll(adapter);
                state.Verify(x => x.GetFixtureState("ABC"), Times.AtLeastOnce());
            }

            adapter.Stop();

        }

        [Test]
        [Category("Adapter")]
        public void AdapterIsDisposedCorrectlyTest()
        {
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


            Adapter adapter = new Adapter(settings.Object, service.Object, connector.Object);
            adapter.StreamCreated += adapter_StreamCreated;

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

        private static void adapter_StreamCreated(object sender, System.EventArgs e)
        {
            lock (sender)
            {
                Monitor.Wait(sender);
            }
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

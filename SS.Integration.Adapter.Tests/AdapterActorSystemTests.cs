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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Routing;
using Akka.TestKit.NUnit;
using Moq;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    /// <summary>
    /// Tests for the actor system configuration built by <see cref="AdapterActorSystem"/>.
    /// The actor system used by these tests is created with the same configuration the adapter uses at runtime,
    /// so the actors are created exactly as <see cref="AdapterActorSystem.Init"/> creates them.
    /// </summary>
    [TestFixture]
    public class AdapterActorSystemTests : TestKit
    {
        #region Constants

        public const string ADAPTER_ACTOR_SYSTEM_CATEGORY = nameof(AdapterActorSystemTests);

        private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);

        #endregion

        #region Fields

        private Mock<ISettings> _settingsMock;
        private Mock<IStoreProvider> _storeProviderMock;
        private Mock<IServiceFacade> _serviceFacadeMock;

        #endregion

        #region Constructors

        public AdapterActorSystemTests()
            : base(AdapterActorSystem.BuildConfig())
        {
        }

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            _settingsMock = new Mock<ISettings>();
            _settingsMock.SetupGet(a => a.StateProviderPath).Returns(GetType().Assembly.Location);
            _settingsMock.SetupGet(a => a.FixturesStateFilePath).Returns("fixturesState.json");
            _settingsMock.SetupGet(a => a.FixturesStateAutoStoreInterval).Returns(1000);
            _settingsMock.SetupGet(a => a.FixtureCreationConcurrency).Returns(3);

            _storeProviderMock = new Mock<IStoreProvider>();

            _serviceFacadeMock = new Mock<IServiceFacade>();
            _serviceFacadeMock.Setup(o => o.GetSports()).Returns(new List<IFeature>());
        }

        #endregion

        #region Test Methods - FixtureStateActor dispatcher

        /// <summary>
        /// This test ensures the default configuration defines the FixtureStateActor dispatcher as a PinnedDispatcher
        /// (one dedicated thread) and assigns it to the actor through akka.actor.deployment,
        /// so the state lookups never compete with the stream listeners for a thread.
        /// </summary>
        [Test]
        [Category(ADAPTER_ACTOR_SYSTEM_CATEGORY)]
        public void GivenDefaultConfigurationThenFixtureStateDispatcherIsPinnedAndAssignedToTheActor()
        {
            //
            //Arrange
            //
            var config = AdapterActorSystem.BuildConfig();

            //
            //Act
            //
            var dispatcherConfig = config.GetConfig(FixtureStateActor.DispatcherId);
            var deploymentConfig = config.GetConfig($"akka.actor.deployment./{FixtureStateActor.ActorName}");
            var dispatcher = Sys.Dispatchers.Lookup(FixtureStateActor.DispatcherId);

            //
            //Assert
            //
            Assert.IsNotNull(dispatcherConfig, $"{FixtureStateActor.DispatcherId} is not defined in the configuration");
            Assert.AreEqual("PinnedDispatcher", dispatcherConfig.GetString("type"));
            Assert.IsInstanceOf<PinnedDispatcher>(dispatcher);
            Assert.IsNotNull(deploymentConfig, $"no akka.actor.deployment entry for /{FixtureStateActor.ActorName}");
            Assert.AreEqual(FixtureStateActor.DispatcherId, deploymentConfig.GetString("dispatcher"));
        }

        /// <summary>
        /// This test ensures a FixtureStateActor created the same way AdapterActorSystem creates it (no dispatcher
        /// set in code) is placed on its dedicated dispatcher by the deployment configuration and answers lookups.
        /// </summary>
        [Test]
        [Category(ADAPTER_ACTOR_SYSTEM_CATEGORY)]
        public void GivenFixtureStateActorCreatedByNameThenItRunsOnTheDedicatedDispatcherAndAnswersLookups()
        {
            //
            //Arrange
            //
            var fixtureStateActor = CreateFixtureStateActor();

            //
            //Act
            //
            var fixtureState =
                fixtureStateActor
                    .Ask<FixtureState>(new GetFixtureStateMsg { FixtureId = "unknownFixtureId" }, AskTimeout)
                    .Result;
            var actorCell = ((ActorRefWithCell)fixtureStateActor).Underlying as ActorCell;

            //
            //Assert
            //
            Assert.IsNull(fixtureState);
            Assert.IsNotNull(actorCell);
            Assert.AreEqual(FixtureStateActor.DispatcherId, actorCell.Dispatcher.Id);
            Assert.IsInstanceOf<PinnedDispatcher>(actorCell.Dispatcher);
        }

        /// <summary>
        /// This test reproduces the failure mechanism seen at kick-off surges: the .NET thread pool is fully
        /// occupied by blocked work, so anything queued on it does not run. The FixtureStateActor must still answer
        /// GetFixtureStateMsg well within the 10s Ask timeout used by the stream listeners, because it no longer
        /// depends on a pool thread.
        /// </summary>
        [Test]
        [Category(ADAPTER_ACTOR_SYSTEM_CATEGORY)]
        public void GivenStarvedThreadPoolThenFixtureStateActorStillAnswersLookups()
        {
            //
            //Arrange
            //
            var fixtureStateActor = CreateFixtureStateActor();
            //make sure the actor is started before the pool is starved
            fixtureStateActor
                .Ask<FixtureState>(new GetFixtureStateMsg { FixtureId = "warmUp" }, AskTimeout)
                .Wait();

            using (ThreadPoolStarvation.Start())
            {
                //
                //Act
                //
                var lookup = fixtureStateActor.Ask<FixtureState>(
                    new GetFixtureStateMsg { FixtureId = "unknownFixtureId" },
                    AskTimeout);
                var answered = lookup.Wait(AskTimeout);

                //
                //Assert
                //
                Assert.IsTrue(answered, "FixtureStateActor did not answer while the thread pool was starved");
                Assert.IsNull(lookup.Result);
            }
        }

        #endregion

        #region Test Methods - SportProcessorRouterActor dispatcher

        /// <summary>
        /// This test ensures the default configuration defines the SportProcessorRouterActor dispatcher as a
        /// ForkJoinDispatcher with its own dedicated threads, sized to FixtureCreationConcurrency (one thread per
        /// routee), and without deadlock detection (a routee legitimately holds its thread for a whole HTTP call).
        /// </summary>
        [Test]
        [Category(ADAPTER_ACTOR_SYSTEM_CATEGORY)]
        public void GivenSettingsThenSportProcessorDispatcherIsForkJoinSizedToFixtureCreationConcurrency()
        {
            //
            //Arrange
            //
            _settingsMock.SetupGet(a => a.FixtureCreationConcurrency).Returns(7);

            //
            //Act
            //
            var config = AdapterActorSystem.BuildConfig(_settingsMock.Object);
            var dispatcherConfig = config.GetConfig(SportProcessorRouterActor.DispatcherId);
            var dispatcher = Sys.Dispatchers.Lookup(SportProcessorRouterActor.DispatcherId);

            //
            //Assert
            //
            Assert.IsNotNull(dispatcherConfig, $"{SportProcessorRouterActor.DispatcherId} is not defined in the configuration");
            Assert.AreEqual("ForkJoinDispatcher", dispatcherConfig.GetString("type"));
            Assert.AreEqual(7, dispatcherConfig.GetInt("dedicated-thread-pool.thread-count"));
            Assert.IsFalse(dispatcherConfig.HasPath("dedicated-thread-pool.deadlock-timeout"), "deadlock detection must stay off");
            Assert.IsNotNull(dispatcher);
            Assert.AreEqual(SportProcessorRouterActor.DispatcherId, dispatcher.Id);
        }

        /// <summary>
        /// This test ensures the SportProcessorRouterActor pool created the same way AdapterActorSystem creates it
        /// runs both the router and every routee on the dedicated dispatcher.
        /// </summary>
        [Test]
        [Category(ADAPTER_ACTOR_SYSTEM_CATEGORY)]
        public void GivenSportProcessorRouterThenRouterAndRouteesRunOnTheDedicatedDispatcher()
        {
            //
            //Arrange
            //
            var router = CreateSportProcessorRouter();

            //
            //Act
            //
            var routees = router.Ask<Routees>(new GetRoutees(), AskTimeout).Result;
            var routerCell = ((ActorRefWithCell)router).Underlying as ActorCell;
            var routeeDispatcherIds = routees.Members
                .OfType<ActorRefRoutee>()
                .Select(r => (((ActorRefWithCell)r.Actor).Underlying as ActorCell)?.Dispatcher.Id)
                .ToArray();

            //
            //Assert
            //
            Assert.IsNotNull(routerCell);
            Assert.AreEqual(SportProcessorRouterActor.DispatcherId, routerCell.Dispatcher.Id, "router dispatcher");
            Assert.AreEqual(3, routeeDispatcherIds.Length, "routee count");
            Assert.That(routeeDispatcherIds, Is.All.EqualTo(SportProcessorRouterActor.DispatcherId), "routee dispatchers");
        }

        /// <summary>
        /// This test reproduces the kick-off starvation for the resource sweep: with the .NET thread pool fully
        /// occupied, a ProcessSportMsg sent to the SportProcessorRouterActor must still be routed and reach the
        /// UDAPI facade (GetSports) within the Ask timeout, because neither the router nor the routee needs a pool thread.
        /// </summary>
        [Test]
        [Category(ADAPTER_ACTOR_SYSTEM_CATEGORY)]
        public void GivenStarvedThreadPoolThenSportProcessorRouterStillProcessesTheSportsSweep()
        {
            //
            //Arrange
            //
            var getSportsCalled = new ManualResetEventSlim(false);
            _serviceFacadeMock
                .Setup(o => o.GetSports())
                .Callback(() => getSportsCalled.Set())
                .Returns(new List<IFeature>());
            var router = CreateSportProcessorRouter();
            //make sure the router and routees are started before the pool is starved
            router.Ask<Routees>(new GetRoutees(), AskTimeout).Wait();

            using (ThreadPoolStarvation.Start())
            {
                //
                //Act
                //
                router.Tell(new ProcessSportMsg());
                var processed = getSportsCalled.Wait(AskTimeout);

                //
                //Assert
                //
                Assert.IsTrue(processed, "SportProcessorRouterActor did not process ProcessSportMsg while the thread pool was starved");
            }
        }

        #endregion

        #region Test Methods - StreamListenerActor bulkhead dispatcher

        /// <summary>
        /// This test ensures the default configuration defines the StreamListenerActor bulkhead dispatcher as a
        /// ForkJoinDispatcher with its own bounded set of threads and without deadlock detection (a listener legitimately
        /// holds its thread for a whole snapshot, plug-in call or AMQP RPC).
        /// </summary>
        [Test]
        [Category(ADAPTER_ACTOR_SYSTEM_CATEGORY)]
        public void GivenDefaultConfigurationThenStreamListenerDispatcherIsBoundedForkJoin()
        {
            //
            //Arrange
            //
            var config = AdapterActorSystem.BuildConfig();

            //
            //Act
            //
            var dispatcherConfig = config.GetConfig(StreamListenerActor.DispatcherId);
            var dispatcher = Sys.Dispatchers.Lookup(StreamListenerActor.DispatcherId);

            //
            //Assert
            //
            Assert.IsNotNull(dispatcherConfig, $"{StreamListenerActor.DispatcherId} is not defined in the configuration");
            Assert.AreEqual("ForkJoinDispatcher", dispatcherConfig.GetString("type"));
            Assert.AreEqual(AdapterActorSystem.DefaultStreamListenerThreads, dispatcherConfig.GetInt("dedicated-thread-pool.thread-count"));
            Assert.IsFalse(dispatcherConfig.HasPath("dedicated-thread-pool.deadlock-timeout"), "deadlock detection must stay off");
            Assert.IsNotNull(dispatcher);
            Assert.AreEqual(StreamListenerActor.DispatcherId, dispatcher.Id);
        }

        /// <summary>
        /// This test ensures the deployment entries place exactly the right actors on the bulkhead: every child of the
        /// StreamListenerManagerActor (the stream listeners) and each listener's ResourceActor, while the
        /// StreamListenerBuilderActor (explicit entry, sibling of the listeners) and the per-fixture StreamHealthCheckActor
        /// and StreamStatsActor (grandchildren, not matched by the wildcard) stay on the default dispatcher.
        /// The actors are created by name only, with no dispatcher in their Props, as the adapter creates them.
        /// </summary>
        [Test]
        [Category(ADAPTER_ACTOR_SYSTEM_CATEGORY)]
        public void GivenStreamListenerActorTreeCreatedByNameThenOnlyListenersAndResourceActorsRunOnTheBulkhead()
        {
            //
            //Arrange
            //
            var manager = Sys.ActorOf(Props.Create(() => new TreeActor()), StreamListenerManagerActor.ActorName);

            //
            //Act
            //
            var listener = CreateChild(manager, StreamListenerActor.GetName("fixture1"));
            var builder = CreateChild(manager, StreamListenerBuilderActor.ActorName);
            var resource = CreateChild(listener, ResourceActor.ActorName);
            var healthCheck = CreateChild(listener, StreamHealthCheckActor.ActorName);
            var stats = CreateChild(listener, StreamStatsActor.ActorName);

            //
            //Assert
            //
            Assert.AreEqual(StreamListenerActor.DispatcherId, DispatcherIdOf(listener), "stream listener");
            Assert.AreEqual(StreamListenerActor.DispatcherId, DispatcherIdOf(resource), "listener's ResourceActor");
            Assert.AreEqual(Dispatchers.DefaultDispatcherId, DispatcherIdOf(builder), "StreamListenerBuilderActor must stay on the default dispatcher");
            Assert.AreEqual(Dispatchers.DefaultDispatcherId, DispatcherIdOf(healthCheck), "StreamHealthCheckActor must stay on the default dispatcher");
            Assert.AreEqual(Dispatchers.DefaultDispatcherId, DispatcherIdOf(stats), "StreamStatsActor must stay on the default dispatcher");
            Assert.AreEqual(Dispatchers.DefaultDispatcherId, DispatcherIdOf(manager), "StreamListenerManagerActor itself");
        }

        /// <summary>
        /// This test reproduces the kick-off surge on the listeners' side: more stream listeners than the bulkhead has
        /// threads all block at once inside their handlers. The .NET thread pool must stay free (an actor on the default
        /// dispatcher still answers), the FixtureStateActor must still answer, and a listener that receives a burst of
        /// messages while the bulkhead is saturated must process them in order once it gets a thread.
        /// </summary>
        [Test]
        [Category(ADAPTER_ACTOR_SYSTEM_CATEGORY)]
        public void GivenMoreBlockedListenersThanBulkheadThreadsThenDefaultDispatcherAndFixtureStateStillRespondAndOrderHolds()
        {
            //
            //Arrange
            //
            var manager = Sys.ActorOf(Props.Create(() => new TreeActor()), StreamListenerManagerActor.ActorName);
            var blockedListeners = Enumerable.Range(0, AdapterActorSystem.DefaultStreamListenerThreads + 8)
                .Select(i => CreateChild(manager, StreamListenerActor.GetName($"blocked{i}")))
                .ToList();
            var orderedListener = CreateChild(manager, StreamListenerActor.GetName("ordered"));
            var defaultDispatcherProbe = Sys.ActorOf(Props.Create(() => new TreeActor()), "defaultDispatcherProbe");
            var fixtureStateActor = CreateFixtureStateActor();
            var gate = new ManualResetEventSlim(false);

            try
            {
                //
                //Act
                //
                foreach (var blockedListener in blockedListeners)
                {
                    blockedListener.Tell(new TreeActor.Block(gate));
                }
                //the burst arrives while the bulkhead is saturated
                for (var i = 1; i <= 200; i++)
                {
                    orderedListener.Tell(i);
                }
                Thread.Sleep(500);

                var probeAnswered = defaultDispatcherProbe.Ask<string>("ping", AskTimeout).Wait(AskTimeout);
                var fixtureStateAnswered = fixtureStateActor
                    .Ask<FixtureState>(new GetFixtureStateMsg { FixtureId = "unknownFixtureId" }, AskTimeout)
                    .Wait(AskTimeout);

                //
                //Assert
                //
                Assert.IsTrue(probeAnswered, "an actor on the default dispatcher did not answer while the listeners were blocked");
                Assert.IsTrue(fixtureStateAnswered, "FixtureStateActor did not answer while the listeners were blocked");
            }
            finally
            {
                gate.Set();
            }

            //
            //Assert - order
            //
            var received = orderedListener.Ask<int[]>(new TreeActor.Dump(), AskTimeout).Result;
            Assert.AreEqual(Enumerable.Range(1, 200).ToArray(), received, "per-fixture message order");
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Asks a TreeActor to create a child by name (no dispatcher in the child's Props) and waits until the child answers.
        /// </summary>
        private IActorRef CreateChild(IActorRef parent, string name)
        {
            var child = parent.Ask<IActorRef>(new TreeActor.CreateChild(name), AskTimeout).Result;
            child.Ask<string>("ping", AskTimeout).Wait();
            return child;
        }

        private static string DispatcherIdOf(IActorRef actorRef)
        {
            var cell = ((ActorRefWithCell)actorRef).Underlying as ActorCell;
            Assert.IsNotNull(cell, $"{actorRef.Path} has no started cell");
            return cell.Dispatcher.Id;
        }

        /// <summary>
        /// Stand-in for the adapter's actor tree: creates named children on request (each one another TreeActor),
        /// answers pings, blocks its thread on a gate when told to, and records integers it receives.
        /// </summary>
        public class TreeActor : ReceiveActor
        {
            public class CreateChild
            {
                public CreateChild(string name) { Name = name; }
                public string Name { get; }
            }

            public class Block
            {
                public Block(ManualResetEventSlim gate) { Gate = gate; }
                public ManualResetEventSlim Gate { get; }
            }

            public class Dump
            {
            }

            private readonly List<int> _received = new List<int>();

            public TreeActor()
            {
                Receive<CreateChild>(m => Sender.Tell(Context.ActorOf(Props.Create(() => new TreeActor()), m.Name), Self));
                Receive<Block>(m => m.Gate.Wait());
                Receive<int>(m => _received.Add(m));
                Receive<Dump>(m => Sender.Tell(_received.ToArray(), Self));
                Receive<string>(m => Sender.Tell(m, Self));
            }
        }


        /// <summary>
        /// Creates the FixtureStateActor the same way AdapterActorSystem does: by name, with no dispatcher set in code,
        /// so the dispatcher comes from akka.actor.deployment.
        /// </summary>
        private IActorRef CreateFixtureStateActor()
        {
            return Sys.ActorOf(
                Props.Create(() =>
                    new FixtureStateActor(
                        _settingsMock.Object,
                        _storeProviderMock.Object)),
                FixtureStateActor.ActorName);
        }

        /// <summary>
        /// Creates the SportProcessorRouterActor pool with the same Props AdapterActorSystem uses.
        /// </summary>
        private IActorRef CreateSportProcessorRouter()
        {
            return Sys.ActorOf(
                AdapterActorSystem.SportProcessorRouterProps(
                    _serviceFacadeMock.Object,
                    _settingsMock.Object.FixtureCreationConcurrency),
                SportProcessorRouterActor.ActorName);
        }

        /// <summary>
        /// Caps the .NET thread pool at the processor count and occupies every worker thread with blocked work,
        /// so nothing queued on the pool runs until disposed. Verifies the starvation with a probe before returning.
        /// </summary>
        private sealed class ThreadPoolStarvation : IDisposable
        {
            private readonly int _originalMinWorker;
            private readonly int _originalMinIo;
            private readonly int _originalMaxWorker;
            private readonly int _originalMaxIo;
            private readonly ManualResetEventSlim _releaseBlockers = new ManualResetEventSlim(false);
            private readonly List<Task> _blockers = new List<Task>();

            private ThreadPoolStarvation()
            {
                ThreadPool.GetMinThreads(out _originalMinWorker, out _originalMinIo);
                ThreadPool.GetMaxThreads(out _originalMaxWorker, out _originalMaxIo);
            }

            public static ThreadPoolStarvation Start()
            {
                var starvation = new ThreadPoolStarvation();
                try
                {
                    var poolSize = Environment.ProcessorCount;
                    Assert.IsTrue(ThreadPool.SetMinThreads(poolSize, starvation._originalMinIo), "could not set min pool threads");
                    Assert.IsTrue(ThreadPool.SetMaxThreads(poolSize, starvation._originalMaxIo), "could not set max pool threads");

                    //occupy every worker thread the pool is allowed to have, plus a few queued behind them
                    for (var i = 0; i < poolSize + 4; i++)
                    {
                        starvation._blockers.Add(Task.Run(() => starvation._releaseBlockers.Wait()));
                    }
                    //sanity check: the pool really is starved, ordinary pool work does not get a thread
                    var probe = Task.Run(() => true);
                    Assert.IsFalse(probe.Wait(TimeSpan.FromMilliseconds(500)), "thread pool is not starved, test setup is invalid");

                    return starvation;
                }
                catch
                {
                    starvation.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                _releaseBlockers.Set();
                ThreadPool.SetMaxThreads(_originalMaxWorker, _originalMaxIo);
                ThreadPool.SetMinThreads(_originalMinWorker, _originalMinIo);
                Task.WaitAll(_blockers.ToArray(), TimeSpan.FromSeconds(5));
            }
        }

        #endregion
    }
}

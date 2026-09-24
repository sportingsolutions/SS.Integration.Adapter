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
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Dispatch;
using Akka.TestKit.NUnit;
using Moq;
using NUnit.Framework;
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

            _storeProviderMock = new Mock<IStoreProvider>();
        }

        #endregion

        #region Test Methods

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

            ThreadPool.GetMinThreads(out var originalMinWorker, out var originalMinIo);
            ThreadPool.GetMaxThreads(out var originalMaxWorker, out var originalMaxIo);
            var poolSize = Environment.ProcessorCount;
            var releaseBlockers = new ManualResetEventSlim(false);
            var blockers = new List<Task>();

            try
            {
                Assert.IsTrue(ThreadPool.SetMinThreads(poolSize, originalMinIo), "could not set min pool threads");
                Assert.IsTrue(ThreadPool.SetMaxThreads(poolSize, originalMaxIo), "could not set max pool threads");

                //occupy every worker thread the pool is allowed to have, plus a few queued behind them
                for (var i = 0; i < poolSize + 4; i++)
                {
                    blockers.Add(Task.Run(() => releaseBlockers.Wait()));
                }
                //sanity check: the pool really is starved, ordinary pool work does not get a thread
                var probe = Task.Run(() => true);
                Assert.IsFalse(probe.Wait(TimeSpan.FromMilliseconds(500)), "thread pool is not starved, test setup is invalid");

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
            finally
            {
                releaseBlockers.Set();
                ThreadPool.SetMaxThreads(originalMaxWorker, originalMaxIo);
                ThreadPool.SetMinThreads(originalMinWorker, originalMinIo);
                Task.WaitAll(blockers.ToArray(), TimeSpan.FromSeconds(5));
            }
        }

        #endregion

        #region Private methods

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

        #endregion
    }
}

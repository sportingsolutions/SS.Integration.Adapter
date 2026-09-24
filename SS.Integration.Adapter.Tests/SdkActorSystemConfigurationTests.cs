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
using Akka.Actor;
using Akka.Configuration;
using Akka.Dispatch;
using NUnit.Framework;
using SS.Integration.Adapter.Configuration;

namespace SS.Integration.Adapter.Tests
{
    /// <summary>
    /// Tests for the HOCON the adapter's App.config carries for the SDK's actor system (SDKSystem).
    /// The SDK system reads that HOCON from the application configuration, so these tests build an actor system
    /// from the same text and create actors by name, as the SDK does, with no dispatcher in their Props.
    /// </summary>
    [TestFixture]
    public class SdkActorSystemConfigurationTests
    {
        #region Constants

        public const string SDK_ACTOR_SYSTEM_CONFIGURATION_CATEGORY = nameof(SdkActorSystemConfigurationTests);

        private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(5);

        #endregion

        #region Fields

        private ActorSystem _sdkSystem;

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            _sdkSystem = ActorSystem.Create(
                "SDKSystemTest",
                ConfigurationFactory.ParseString(SdkActorSystemConfiguration.DispatchersHocon));
        }

        [TearDown]
        public void TearDownTest()
        {
            _sdkSystem?.Terminate().Wait(TimeSpan.FromSeconds(10));
            _sdkSystem = null;
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures the HOCON defines pinned dispatchers for the EchoControllerActor and the
        /// StreamControllerActor and assigns them through akka.actor.deployment, so an actor created by name
        /// (no dispatcher in its Props) lands on its own thread.
        /// </summary>
        [Test]
        [Category(SDK_ACTOR_SYSTEM_CONFIGURATION_CATEGORY)]
        public void GivenSdkDispatchersHoconThenEchoAndStreamControllerActorsCreatedByNameRunOnPinnedDispatchers()
        {
            //
            //Arrange
            //
            var echoControllerActor = _sdkSystem.ActorOf(Props.Create(() => new ReplyActor()), SdkActorSystemConfiguration.EchoControllerActorName);
            var streamControllerActor = _sdkSystem.ActorOf(Props.Create(() => new ReplyActor()), SdkActorSystemConfiguration.StreamControllerActorName);

            //
            //Act
            //
            echoControllerActor.Ask<string>("ping", AskTimeout).Wait();
            streamControllerActor.Ask<string>("ping", AskTimeout).Wait();
            var echoCell = ((ActorRefWithCell)echoControllerActor).Underlying as ActorCell;
            var streamCell = ((ActorRefWithCell)streamControllerActor).Underlying as ActorCell;

            //
            //Assert
            //
            Assert.IsNotNull(echoCell);
            Assert.AreEqual(SdkActorSystemConfiguration.EchoControllerDispatcherId, echoCell.Dispatcher.Id);
            Assert.IsInstanceOf<PinnedDispatcher>(echoCell.Dispatcher);
            Assert.IsNotNull(streamCell);
            Assert.AreEqual(SdkActorSystemConfiguration.StreamControllerDispatcherId, streamCell.Dispatcher.Id);
            Assert.IsInstanceOf<PinnedDispatcher>(streamCell.Dispatcher);
            Assert.AreNotSame(echoCell.Dispatcher, streamCell.Dispatcher, "each actor must have its own thread");
        }

        /// <summary>
        /// This test ensures an actor created by name without the deployment entry (any other SDK actor) is not
        /// affected and stays on the default dispatcher.
        /// </summary>
        [Test]
        [Category(SDK_ACTOR_SYSTEM_CONFIGURATION_CATEGORY)]
        public void GivenSdkDispatchersHoconThenOtherActorsStayOnTheDefaultDispatcher()
        {
            //
            //Arrange
            //
            var otherActor = _sdkSystem.ActorOf(Props.Create(() => new ReplyActor()), "UpdateDispatcherActor");

            //
            //Act
            //
            otherActor.Ask<string>("ping", AskTimeout).Wait();
            var cell = ((ActorRefWithCell)otherActor).Underlying as ActorCell;

            //
            //Assert
            //
            Assert.IsNotNull(cell);
            Assert.AreEqual(Dispatchers.DefaultDispatcherId, cell.Dispatcher.Id);
        }

        /// <summary>
        /// This test reproduces the kick-off starvation for the SDK's liveness check: with the .NET thread pool fully
        /// occupied, the actor at /user/EchoControllerActor must still process its messages (the echo tick and the
        /// echo POST run there), because it no longer depends on a pool thread.
        /// </summary>
        [Test]
        [Category(SDK_ACTOR_SYSTEM_CONFIGURATION_CATEGORY)]
        public void GivenStarvedThreadPoolThenEchoControllerActorStillProcessesMessages()
        {
            //
            //Arrange
            //
            var echoControllerActor = _sdkSystem.ActorOf(Props.Create(() => new ReplyActor()), SdkActorSystemConfiguration.EchoControllerActorName);
            //make sure the actor is started before the pool is starved
            echoControllerActor.Ask<string>("warmUp", AskTimeout).Wait();

            using (ThreadPoolStarvation.Start())
            {
                //
                //Act
                //
                var tick = echoControllerActor.Ask<string>("tick", AskTimeout);
                var answered = tick.Wait(AskTimeout);

                //
                //Assert
                //
                Assert.IsTrue(answered, "EchoControllerActor did not process its message while the thread pool was starved");
                Assert.AreEqual("tick", tick.Result);
            }
        }

        #endregion

        #region Private classes

        /// <summary>
        /// Stands in for an SDK actor: answers every message to its sender.
        /// </summary>
        public class ReplyActor : ReceiveActor
        {
            public ReplyActor()
            {
                ReceiveAny(message => Sender.Tell(message, Self));
            }
        }

        #endregion
    }
}

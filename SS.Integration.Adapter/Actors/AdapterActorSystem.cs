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

using Akka.Actor;
using Akka.Event;
using Akka.Routing;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This is the AKKA Actors initialization class, including the root System Actor.
    /// </summary>
    public static class AdapterActorSystem
    {
        #region Fields

        private static ActorSystem _actorSystem;
        private static IActorRef _sportsProcessorActor;
        private static IActorRef _sportProcessorRouterActor;
        private static IActorRef _streamListenerManagerActor;
        private static IActorRef _fixtureStateActor;

        #endregion

        public static ActorSystem ActorSystem => _actorSystem;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="udApiService"></param>
        /// <param name="adapterPlugin"></param>
        /// <param name="stateManager"></param>
        /// <param name="suspensionManager"></param>
        /// <param name="streamHealthCheckValidation"></param>
        /// <param name="fixtureValidation"></param>
        public static void Init(
            ISettings settings,
            IServiceFacade udApiService,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager,
            ISuspensionManager suspensionManager,
            IStreamHealthCheckValidation streamHealthCheckValidation,
            IFixtureValidation fixtureValidation)
        {
            _actorSystem = ActorSystem.Create("AdapterSystem");

            var fileStoreProvider = new FileStoreProvider(settings.StateProviderPath);
            _fixtureStateActor = ActorSystem.ActorOf(
                Props.Create(() =>
                    new FixtureStateActor(
                        settings,
                        fileStoreProvider)),
                FixtureStateActor.ActorName);

            _streamListenerManagerActor = ActorSystem.ActorOf(
                Props.Create(() =>
                    new StreamListenerManagerActor(
                        settings,
                        adapterPlugin,
                        stateManager,
                        suspensionManager,
                        streamHealthCheckValidation,
                        fixtureValidation)),
                StreamListenerManagerActor.ActorName);

            _sportProcessorRouterActor = ActorSystem.ActorOf(
                Props.Create(() => new SportProcessorRouterActor(udApiService))
                    .WithRouter(new SmallestMailboxPool(settings.FixtureCreationConcurrency)),
                SportProcessorRouterActor.ActorName);

            _sportsProcessorActor = ActorSystem.ActorOf(
                Props.Create(() =>
                    new SportsProcessorActor(
                        settings,
                        udApiService,
                        _sportProcessorRouterActor)),
                SportsProcessorActor.ActorName);

            // Setup an actor that will handle deadletter type messages
            var deadletterWatchMonitorProps = Props.Create(() => new AdapterDeadletterMonitorActor());
            var deadletterWatchActorRef = _actorSystem.ActorOf(deadletterWatchMonitorProps, "AdapterDeadletterMonitorActor");

            // subscribe to the event stream for messages of type "DeadLetter"
            _actorSystem.EventStream.Subscribe(deadletterWatchActorRef, typeof(DeadLetter));
        }

        public static void Dispose()
        {
            _actorSystem?.Stop(_sportsProcessorActor);
            _actorSystem?.Stop(_sportProcessorRouterActor);
            _actorSystem?.Stop(_streamListenerManagerActor);
            _actorSystem?.Stop(_fixtureStateActor);
            _actorSystem?.Dispose();
            _actorSystem = null;
        }
    }
}

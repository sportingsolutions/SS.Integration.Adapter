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
using Akka.Event;
using Akka.Routing;
using log4net;
using SS.Integration.Adapter.Actors.Strategy;
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
        private static ILog _logger = LogManager.GetLogger(typeof(AdapterActorSystem).ToString());


        private static ActorSystem _actorSystem;
        private static IActorRef _sportsProcessorActor;
        private static IActorRef _sportProcessorRouterActor;
        private static IActorRef _streamListenerManagerActor;
        private static IActorRef _fixtureStateActor;

        /// <summary>
        /// Default number of dedicated threads for <see cref="SportProcessorRouterActor"/> when no settings are given
        /// (matches the default FixtureCreationConcurrency).
        /// </summary>
        private const int DefaultSportProcessorThreads = 20;

        /// <summary>
        /// Default number of dedicated threads for the StreamListenerActor bulkhead dispatcher. A starting size for
        /// roughly 1,800 fixtures: enough for the listeners that are actually inside a snapshot, plug-in call or AMQP
        /// RPC at the same moment, small enough that the pool is not replaced by an equally unbounded set of threads.
        /// Read it against the FixtureStateActor WriteStateToFile "late" warnings and thread-pool counters at kick-off,
        /// and override it in the application configuration HOCON (stream-listener-dispatcher.dedicated-thread-pool.thread-count).
        /// </summary>
        public const int DefaultStreamListenerThreads = 32;

        /// <summary>
        /// Default HOCON applied as a fallback to the akka section of the application configuration.
        /// 
        /// fixture-state-dispatcher: a PinnedDispatcher (one dedicated thread) assigned to the <see cref="FixtureStateActor"/>
        /// through akka.actor.deployment, so no code change is needed to move the actor to another dispatcher.
        /// Every StreamListenerActor asks the FixtureStateActor for the fixture state (10s timeout) and the actor also
        /// writes the state file to disk every few seconds. If it shares the default dispatcher with the stream
        /// listeners, a thread-pool starvation at a kick-off surge (blocking calls holding pool threads) delays those
        /// lookups until they time out and the listeners treat the streams as disconnected. Isolating the actor on
        /// its own thread removes the FixtureStateActor from that contention.
        /// 
        /// sport-processor-dispatcher: a ForkJoinDispatcher with its own dedicated threads (not the .NET thread pool)
        /// for the <see cref="SportProcessorRouterActor"/> router and its routees. Each routee calls the UDAPI
        /// synchronously (GetSports, GetResources per sport, 60s client timeout) every FixtureCheckerFrequency; on the
        /// default dispatcher that parks up to FixtureCreationConcurrency pool threads and, when the pool is starved,
        /// turns into timeouts and actor restarts. thread-count is sized to FixtureCreationConcurrency so every routee
        /// can run at once; the threads idle otherwise. No deadlock-timeout on purpose: a routee legitimately holds its
        /// thread for the whole HTTP call, and the dedicated thread pool's deadlock detection would abort it.
        /// 
        /// stream-listener-dispatcher: the bulkhead. A ForkJoinDispatcher with a bounded set of its own threads for every
        /// StreamListenerActor and its ResourceActor child, assigned through akka.actor.deployment with a wildcard on the
        /// StreamListenerManagerActor's children plus an explicit entry for */ResourceActor. The listeners' synchronous
        /// work (GetSnapshot HTTP, plug-in calls, StartStreaming/StopStreaming AMQP) then consumes those threads and not
        /// the shared pool; when they are all busy the listeners queue in their mailboxes instead of starving the SDK's
        /// echo check, the RabbitMQ consumer callbacks and the FixtureStateActor. The StreamListenerBuilderActor is a
        /// child of the same parent and is explicitly kept on the default dispatcher: it has no blocking work and it is
        /// the gate for creating new listeners at kick-off, so it must not queue behind them. The per-fixture
        /// StreamHealthCheckActor and StreamStatsActor are grandchildren, not matched by the wildcard, and stay on the
        /// default dispatcher too, so the health check never queues behind the listener it monitors. No deadlock-timeout,
        /// for the same reason as the sport processor. On modern .NET a synchronous wait on HttpClient still needs a pool
        /// thread for the completion, so this frees the pool rather than making listener HTTP self-contained.
        /// 
        /// Any of these blocks can be overridden by defining the same key in the application configuration.
        /// </summary>
        /// <param name="sportProcessorThreads">number of dedicated threads for the SportProcessorRouterActor dispatcher</param>
        /// <param name="streamListenerThreads">number of dedicated threads for the StreamListenerActor bulkhead dispatcher</param>
        /// <returns></returns>
        private static string DefaultHocon(int sportProcessorThreads, int streamListenerThreads)
        {
            return @"
            " + FixtureStateActor.DispatcherId + @" {
                type = PinnedDispatcher
                throughput = 1
            }
            " + SportProcessorRouterActor.DispatcherId + @" {
                type = ForkJoinDispatcher
                executor = fork-join-executor
                throughput = 1
                dedicated-thread-pool {
                    thread-count = " + sportProcessorThreads + @"
                    threadtype = background
                }
            }
            " + StreamListenerActor.DispatcherId + @" {
                type = ForkJoinDispatcher
                executor = fork-join-executor
                throughput = 5
                dedicated-thread-pool {
                    thread-count = " + streamListenerThreads + @"
                    threadtype = background
                }
            }
            akka.actor.deployment {
                /" + FixtureStateActor.ActorName + @" {
                    dispatcher = " + FixtureStateActor.DispatcherId + @"
                }
                ""/" + StreamListenerManagerActor.ActorName + @"/*"" {
                    dispatcher = " + StreamListenerActor.DispatcherId + @"
                }
                ""/" + StreamListenerManagerActor.ActorName + @"/*/" + ResourceActor.ActorName + @""" {
                    dispatcher = " + StreamListenerActor.DispatcherId + @"
                }
                /" + StreamListenerManagerActor.ActorName + @"/" + StreamListenerBuilderActor.ActorName + @" {
                    dispatcher = akka.actor.default-dispatcher
                }
            }";
        }

        #endregion

        public static ActorSystem ActorSystem => _actorSystem;

        /// <summary>
        /// Builds the actor system configuration with the default sizing (see <see cref="BuildConfig(ISettings)"/>).
        /// </summary>
        /// <returns></returns>
        public static Config BuildConfig()
        {
            return BuildConfig(null);
        }

        /// <summary>
        /// Builds the actor system configuration: the akka HOCON section of the application configuration
        /// (the same source ActorSystem.Create(name) uses) with the adapter defaults as fallback,
        /// so the adapter's dedicated dispatchers exist even when the application configuration does not define them.
        /// </summary>
        /// <param name="settings">adapter settings used to size the defaults; null for the built-in default sizing</param>
        /// <returns></returns>
        public static Config BuildConfig(ISettings settings)
        {
            var sportProcessorThreads = settings != null && settings.FixtureCreationConcurrency > 0
                ? settings.FixtureCreationConcurrency
                : DefaultSportProcessorThreads;

            var defaults = ConfigurationFactory.ParseString(DefaultHocon(sportProcessorThreads, DefaultStreamListenerThreads));
            var appConfig = ConfigurationFactory.Load();

            return appConfig == null || appConfig.IsEmpty
                ? defaults
                : appConfig.WithFallback(defaults);
        }

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
            _actorSystem = ActorSystem.Create("AdapterSystem", BuildConfig(settings));

            var fileStoreProvider = new FileStoreProvider(settings.StateProviderPath);
            CreateFixtureStateActor(settings, fileStoreProvider);
            CreateStreamListenerManagerActor(settings, adapterPlugin, stateManager, suspensionManager, streamHealthCheckValidation, fixtureValidation);
            CreateSportProcessorRouterActor(settings, udApiService);
            CreateSportsProcessorActor(settings, udApiService);

            // Setup an actor that will handle deadletter type messages
            var deadletterWatchMonitorProps = Props.Create(() => new AdapterDeadletterMonitorActor());
            var deadletterWatchActorRef = _actorSystem.ActorOf(deadletterWatchMonitorProps, "AdapterDeadletterMonitorActor");

            // subscribe to the event stream for messages of type "DeadLetter"
            _actorSystem.EventStream.Subscribe(deadletterWatchActorRef, typeof(DeadLetter));
        }

        private static void CreateSportsProcessorActor(ISettings settings, IServiceFacade udApiService)
        {
            try
            {
                _sportsProcessorActor = ActorSystem.ActorOf(
                    Props.Create(() =>
                        new SportsProcessorActor(
                            settings,
                            udApiService,
                            _sportProcessorRouterActor)),
                    SportsProcessorActor.ActorName);
            }
            catch (Exception e)
            {
                _logger.Fatal($"Error creating SportsProcessorActor {e}");
                throw;
            }
            
        }

        private static void CreateSportProcessorRouterActor(ISettings settings, IServiceFacade udApiService)
        {
            try
            {
                _sportProcessorRouterActor = ActorSystem.ActorOf(
                    SportProcessorRouterProps(udApiService, settings.FixtureCreationConcurrency),
                    SportProcessorRouterActor.ActorName);
            }
            catch (Exception e)
            {
                _logger.Fatal($"Error creating SportProcessorRouterActor {e}");
                throw;
            }
            
        }

        private static void CreateStreamListenerManagerActor(ISettings settings, IAdapterPlugin adapterPlugin,
            IStateManager stateManager, ISuspensionManager suspensionManager,
            IStreamHealthCheckValidation streamHealthCheckValidation, IFixtureValidation fixtureValidation)
        {
            try
            {
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
            }
            catch (Exception e)
            {
                _logger.Fatal($"Error creating StreamListenerManagerActor {e}");
                throw;
            }

           
        }

        /// <summary>
        /// Props for the SportProcessorRouterActor pool: both the router and its routees run on
        /// <see cref="SportProcessorRouterActor.DispatcherId"/> so that neither the routing of ProcessSportMsg nor the
        /// blocking wait of the synchronous UDAPI calls in the routees holds a .NET thread-pool thread.
        /// (On modern .NET a synchronous wait on HttpClient still needs a pool thread for the completion; the
        /// dispatcher frees the pool for the rest of the process rather than making the sweep independent of it.)
        /// </summary>
        /// <param name="udApiService"></param>
        /// <param name="fixtureCreationConcurrency">number of routees</param>
        /// <returns></returns>
        public static Props SportProcessorRouterProps(IServiceFacade udApiService, int fixtureCreationConcurrency)
        {
            return Props.Create(() => new SportProcessorRouterActor(udApiService))
                .WithDispatcher(SportProcessorRouterActor.DispatcherId)
                .WithRouter(new SmallestMailboxPool(fixtureCreationConcurrency)
                    .WithDispatcher(SportProcessorRouterActor.DispatcherId));
        }

        private static void CreateFixtureStateActor(ISettings settings, FileStoreProvider fileStoreProvider)
        {
            try
            {
                _fixtureStateActor = ActorSystem.ActorOf(
                    Props.Create(() =>
                        new FixtureStateActor(
                            settings,
                            fileStoreProvider)
                    ),
                    FixtureStateActor.ActorName);
            }
            catch (Exception e)
            {
                _logger.Fatal($"Error creating FixtureStateActor {e}");
                throw;
            }
            
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

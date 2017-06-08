using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Interface;
using SS.Integration.Common.Stats;
using SS.Integration.Common.Stats.Interface;
using SS.Integration.Common.Stats.Keys;

namespace SS.Integration.Adapter.Actors
{
    public class FixtureManagerActor : ReceiveActor, IWithUnboundedStash
    {
        public const string ActorName = "FixtureManagerActor";

        private readonly ILog _logger = LogManager.GetLogger(typeof(FixtureManagerActor));
        private readonly ISettings _settings;
        private readonly IServiceFacade _serviceFacade;
        private readonly IStatsHandle _stats;

        public IStash Stash { get; set; }

        public FixtureManagerActor(
            ISettings settings,
            IServiceFacade serviceFacade,
            IActorRef sportProcessorRouterActor)
        {
            _settings = settings;
            _serviceFacade = serviceFacade;

            AvailableState();

            Context.System.Scheduler.ScheduleTellRepeatedly(
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(_settings.FixtureCheckerFrequency),
                Self,
                new ProcessSportsMessage(),
                Self);
            _stats = StatsManager.Instance["adapter.core.fixturemanager"].GetHandle();
        }

        private void AvailableState()
        {
            Receive<ProcessSportsMessage>(o => ProcessSports());
        }

        private void BusyState()
        {
            //Receive<ProcessSportsMessage>(o => Stash.Stash());
        }

        private void ProcessSports()
        {
            var sports = _serviceFacade.GetSports();
        }

        /*private void CreateFixture()
        {
            try
            {
                foreach (var resource in _resourceCreationQueue.GetConsumingEnumerable(_creationQueueCancellationToken.Token))
                {
                    try
                    {
                        _logger.DebugFormat("Task={0} is processing {1} from the queue", Task.CurrentId, resource);

                        if (_streamManager.HasStreamListener(resource.Id))
                            continue;

                        ValidateCache(resource);
                        _streamManager.CreateStreamListener(resource, Adapter.PlatformConnector);
                        _logger.DebugFormat("Task={0} is finished processing {1} from the queue", Task.CurrentId, resource);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(string.Format("There has been a problem creating a listener for {0}", resource), ex);
                    }

                    if (_creationQueueCancellationToken.IsCancellationRequested)
                    {
                        _logger.DebugFormat("Fixture creation task={0} will terminate as requested", Task.CurrentId);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.DebugFormat("Fixture creation task={0} exited as requested", Task.CurrentId);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("An error occured on fixture creation task={0}", Task.CurrentId), ex);
            }

        }*/

        private void GetStatistics()
        {
            var currentlyConnected = _streamListenerManager.ListenersCount;

            try
            {
                _stats.AddValueUnsafe(AdapterCoreKeys.ADAPTER_TOTAL_MEMORY, GC.GetTotalMemory(false).ToString());
                _stats.SetValueUnsafe(AdapterCoreKeys.ADAPTER_HEALTH_CHECK, "1");
                _stats.SetValueUnsafe(AdapterCoreKeys.ADAPTER_FIXTURE_TOTAL, currentlyConnected.ToString());

                foreach (var sport in _streamListenerManager.GetListenersBySport())
                {
                    _stats.SetValueUnsafe(string.Format(AdapterCoreKeys.SPORT_FIXTURE_TOTAL, sport.Key),
                        sport.Count().ToString());
                    _stats.SetValueUnsafe(string.Format(AdapterCoreKeys.SPORT_FIXTURE_STREAMING_TOTAL, sport.Key),
                        sport.Count(x => x.IsStreaming).ToString());
                    _stats.SetValueUnsafe(string.Format(AdapterCoreKeys.SPORT_FIXTURE_IN_PLAY_TOTAL, sport.Key),
                        sport.Count(x => x.IsInPlay).ToString());
                }
            }
            catch
            {
            }

            _logger.DebugFormat($"Currently adapter is streaming fixtureCount={currentlyConnected}");
        }

        private class ProcessSportsMessage
        {
        }
    }
}

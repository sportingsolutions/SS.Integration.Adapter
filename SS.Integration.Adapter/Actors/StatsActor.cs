using System;
using System.Linq;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Interface;
using SS.Integration.Common.Stats;
using SS.Integration.Common.Stats.Interface;
using SS.Integration.Common.Stats.Keys;

namespace SS.Integration.Adapter.Actors
{
    public class StatsActor : ReceiveActor
    {
        public const string ActorName = "StatsActor";

        private readonly ILog _logger = LogManager.GetLogger(typeof(StatsActor));
        private readonly IStatsHandle _stats;

        public StatsActor()
        {
            _stats = StatsManager.Instance["adapter.core.stats"].GetHandle();

            Receive<ProcessStatistics>(o => GetStatistics());
        }

        private void GetStatistics()
        {
            _logger.Debug("Start GetStatistics");

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
    }

    internal class ProcessStatistics
    {
    }
}

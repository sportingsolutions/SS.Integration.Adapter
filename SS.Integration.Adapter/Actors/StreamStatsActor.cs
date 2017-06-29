using System;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Actors
{
    public class StreamStatsActor : ReceiveActor
    {
        private ILog _logger = LogManager.GetLogger(typeof(StreamStatsActor));

        private StartUpdateStatsMsg _startMessage;
        // This actor main responsibility is logging how long did it take to process stream update with how many markets 
        // Snapshots same as above
        // any errors should be also counted and logged by fixture

        public StreamStatsActor()
        {
            Receive<StartUpdateStatsMsg>(x => StartLogging(x));
            Receive<FinishedProcessingUpdateStatsMsg>(x => FinishedProcessing(x));
        }

        private void FinishedProcessing(FinishedProcessingUpdateStatsMsg finishedProcessingUpdateStatsMsg)
        {
            var timeTaken = finishedProcessingUpdateStatsMsg.CompletedAt - _startMessage.UpdateReceivedAt;

            var updateOrSnapshot = _startMessage.IsSnapshot ? "Snapshot" : "Update";

            _logger.Info($"{updateOrSnapshot} for {_startMessage.Fixture}, took processingTime={timeTaken.TotalSeconds} seconds at sequence={_startMessage.Sequence}");
        }

        private void StartLogging(StartUpdateStatsMsg startUpdateStatsMsg)
        {
            _startMessage = startUpdateStatsMsg;
        }
    }

    public class StartUpdateStatsMsg
    {
        public bool IsSnapshot { get; set; }
        public DateTime UpdateReceivedAt { get; set; }
        public int Sequence { get; set; }
        public Fixture Fixture { get; set; }
    }

    public class FinishedProcessingUpdateStatsMsg
    {
        public DateTime CompletedAt { get; set; }
    }
}

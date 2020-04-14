using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using log4net;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Actors.Messages;

namespace SS.Integration.Adapter.Helpers
{
    public class StreamStats
    {
        #region Properties

        //internal int SnapshotsCount => _snapshotsCount;
        //internal int StreamUpdatesCount => _streamUpdatesCount;
        //internal Dictionary<string, int> ErrorsCount => _errorsCount;
        //internal int DisconnectionsCount => _disconnectionsCount;
        //internal DateTime LastDisconnectedDate => _lastDisconnectedDate;

        #endregion

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamStats));
        private int _snapshotsCount;
        private int _streamUpdatesCount;
        private static readonly DateTime AdapterStartDate = DateTime.UtcNow;

        private Dictionary<int, UpdateProcessing> messages = new Dictionary<int, UpdateProcessing>();
        
        #endregion


        internal  void AdapterProcessingStarted(UpdateProcessing msg)
        {
            _logger.Debug($"{new StackFrame().GetMethod()?.Name} {msg}");
            messages[0] = msg;
        }

        internal void PluginProcessingStarted(UpdateProcessing msg)
        {
            _logger.Debug($"{new StackFrame().GetMethod()?.Name} {msg}");
            messages[1] = msg;
        }


        internal void PluginProcessingFinished(UpdateProcessing msg)
        {
            _logger.Debug($"{new StackFrame().GetMethod()?.Name} {msg}");
            messages[2] = msg;
        }


        internal void AdapterProcessingFinished(UpdateProcessing msg)
        {
            _logger.Debug($"{new StackFrame().GetMethod()?.Name} {msg}");
            messages[3] = msg;

            if (!ValidateMessages())
                return;


            if ( messages[0].IsSnapshot)
                _snapshotsCount++;
            else
                _streamUpdatesCount++;

            var timeTaken =  msg.Time - messages[0].Time;
            var timeTakenPlugin = messages[2].Time - messages[1].Time;
            _logger.Info($"Adapter { messages[0].PluginMethod} for { messages[0].FixtureName}, took processingTime={timeTaken.TotalSeconds.ToString("N")} sec. pluginProcessingTime={timeTakenPlugin.TotalSeconds.ToString("N")} sec.");
            
            var minutes = (int)Math.Ceiling((DateTime.UtcNow - AdapterStartDate).TotalMinutes);
            if (minutes == 0)
            {
                ClearMessages();
                return;
            }

            var _snapshots_perminute = _snapshotsCount == 0 ? 0 : _snapshotsCount / minutes;
            var _streamupdates_perminute = _streamUpdatesCount == 0 ? 0 : _streamUpdatesCount / minutes;
            _logger.Info($"{ messages[0].FixtureName} -> Snapshots_Processed={_snapshotsCount}  StreamUpdates_Processed={_streamUpdatesCount} Snapshots_PerMinute={_snapshots_perminute} StreamUpdates_PerMinute={_streamupdates_perminute}");
            ClearMessages();
        }

        internal void AdapterProcessingInterrupted()
        {
            if (messages.Any())
            {
                _logger.Warn($"Adapter  { messages[0].PluginMethod} for { messages[0].FixtureName}, was interrupted");
                ClearMessages();
            }
        }

        private bool ValidateMessages()
        {
            if (messages.Count != 4)
            {
                _logger.Warn($"ValidateMessages failed as Messages count={messages.Count} incorrect {messages.FirstOrDefault().Value?.FixtureName}");
                return false;
            }

            if (messages.Select(_ => _.Value.Sequence).Distinct().Count() != 1)
            {
                _logger.Warn($"ValidateMessages failed as Messages Sequence mismatch {messages.FirstOrDefault().Value?.FixtureName}");
                return false;
            }

            return true;
        }

        private void ClearMessages()
        {
           messages.Clear();
        }


        //private void StreamDisconnectedMsgHandler(StreamDisconnectedMsg msg)
        //{
        //    _disconnectionsCount++;
        //    _lastDisconnectedDate = DateTime.UtcNow;

        //    var days = (DateTime.UtcNow - AdapterStartDate).TotalDays;
        //    var weeks = days > 7 ? (int)(days / 7) : 1;

        //    _logger.Info($"Stream got disconnected at {_lastDisconnectedDate}");
        //    _logger.Info($"Detected {_disconnectionsCount / weeks} Stream disconnections / week");
        //}


    }
}

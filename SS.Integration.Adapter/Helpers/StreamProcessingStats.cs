using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using Akka.Event;
using log4net;
using SS.Integration.Adapter.Actors;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.Model.ProcessState;

namespace SS.Integration.Adapter.Helpers
{
    public class StreamProcessingStats
    {
        #region Properties

	    private bool isProcessingUpdate;
	    private ProcessingMetrics context;

		

		#endregion

		#region Fields


		public bool IsProcessingUpdate
		{
			get => isProcessingUpdate;
		}

        private readonly ILog _logger = LogManager.GetLogger(typeof(StreamProcessingStats));
        private int _snapshotsCount;
        private int _streamUpdatesCount;
        private readonly Dictionary<string, int> _errorsCount;
        private DateTime _lastDisconnectedDate;
        private int _disconnectionsCount;
        private static readonly DateTime AdapterStartDate = DateTime.UtcNow;

        

		#endregion


	    internal void StreamUpdateReceived(DateTime pickupFromQueueTime, DateTime receivedByAdapterAt, string header)
	    {
		    _logger.Info($"stream update arrived {header}");
			context = new ProcessingMetrics()
			{
				PickupFromQueue = pickupFromQueueTime,
				AdapterPickup = receivedByAdapterAt,
				StreamUpdateMsgReceived = DateTime.UtcNow,
			};
	    }

	    internal void ProcessingFinished(Fixture fixture)
	    {
		    _logger.Info($"stream update processing finished {fixture.ToStringShort()}");
			if (!context.Cancelled.HasValue && !context.Errored.HasValue)
				context.Completed = DateTime.UtcNow;
		    LogProcessing(fixture);
		    context = null;

	    }

	    private void LogProcessing(Fixture fixture)
	    {
			_logger.Info($"{fixture.ToStringShort()} {context}");
		}

	    internal void MessageDeserialized(Fixture fixture)
	    {
		    _logger.Info($"stream update deserialized {fixture.ToStringShort()}");
			context.Sequence = fixture.Sequence;
			context.MessageDeserialized = DateTime.UtcNow;
	    }

	    

		internal void SuspendingCalled(SuspensionReason reason, string header)
	    {
		    _logger.Info($"Suspending  {header} due reason={reason}");
		    context?.Actions.Add( new FixtureActionMetrics("Suspend", DateTime.UtcNow)
		    {
			    PluginTime = DateTime.UtcNow,
		    });
	    }

	    internal void ActionCalled(string header, string action, bool isOnPlugin)
	    {
		    _logger.Info($"{action} called {header}");
		    context?.Actions.Add(new FixtureActionMetrics(action, DateTime.UtcNow)
		    {
				PluginTime = isOnPlugin ? DateTime.UtcNow : (DateTime?)null

			});
	    }


		internal void ActionFinished(string header, string action)
	    {
		    _logger.Info($"{action} finished {header}");
		    if (context != null)
		    {
			    context.CurrentAction.Completed = DateTime.UtcNow;
		    }
		}

	    internal void ActionErored(string header, string action, Exception e)
	    {
		    _logger.Warn($"{action} failed {header} {e}");
		    if (context != null)
		    {
			    context.CurrentAction.Errored = DateTime.UtcNow;
			    context.CurrentAction.Reason = e.ToString().Take(100).ToString();
		    }
		}

	    internal void ProcessingCancelledSequnceOutdated(Fixture fixture)
	    {
			_logger.Warn($"Fixture processing will be interrupted due to sequnce outdated {fixture.ToStringShort()}");
		    if (context != null)
		    {
			    context.Cancelled = DateTime.UtcNow;
			    context.Reason = "update sequnce outdated";

				context.CurrentAction.Cancelled = DateTime.UtcNow;
			    context.CurrentAction.Reason = "update sequnce outdated";
			}
	    }

		internal void ProcessingCancelledTimeOutdated(Fixture fixture)
		{
			var reason = "update Delay exceeded the limit";
			_logger.Warn($"Fixture processing will be interrupted due to {reason} {fixture.ToStringShort()}");
			if (context != null)
			{
				context.Cancelled = DateTime.UtcNow;
				context.Reason = reason;

				context.CurrentAction.Cancelled = DateTime.UtcNow;
				context.CurrentAction.Reason = reason;
			}

		}

		

	   

	    private string ActionName(bool isFullSnapshot) => isFullSnapshot ? "Snapshot" : "Update";

		internal void PushToPlugin(Fixture snapshot, bool isFullSnapshot)
	    {
		    _logger.Info($"Processing {ActionName(isFullSnapshot)} for {snapshot.ToStringShort()}");
			
		    context?.Actions.Add(new FixtureActionMetrics(ActionName(isFullSnapshot), DateTime.UtcNow));
	    }

	    internal void PushToPluginCancelled(Fixture fixture, string reason)
	    {
		    _logger.Info($"Fixture PushToPlugin cancelled due to \"{reason}\" {fixture.ToStringShort()}");
		    if (context != null)
		    {
				context.CurrentAction.Cancelled = DateTime.UtcNow;
			    context.CurrentAction.Reason = reason;
		    }
	    }


	    internal void PluginProcessingStarted(Fixture fixture)
	    {
		    _logger.Info($"Fixture Plugin processing Started {fixture.ToStringShort()}");
		    if (context != null)
		    {
			    context.CurrentAction.PluginTime = DateTime.UtcNow;
		    }

		}

	    internal void PluginProcessingErrored(Fixture fixture, Exception ex)
	    {
		    _logger.Warn($"Fixture Plugin processing Errored {fixture.ToStringShort()} , {ex} ");
		    if (context != null)
		    {
			    context.CurrentAction.Errored = DateTime.UtcNow;
			    context.CurrentAction.Reason = "Error on plugin side";
		    }

	    }

	    internal void PluginProcessingFinished(Fixture fixture)
	    {
		    _logger.Info($"Fixture Plugin processing finished {fixture.ToStringShort()}");
		    if (context != null)
		    {
			    context.CurrentAction.Completed = DateTime.UtcNow;
		    }
	    }

	    

		internal void ProcessingCancelled(Fixture fixture, string reason)
		{
			_logger.Warn($"Fixture processing cancelled due to \"{reason}\" {fixture.ToStringShort()}");
			if (context != null)
			{
				context.Cancelled = DateTime.UtcNow;
				context.Reason = reason;
			}
		}

	    internal void ProcessingErrored(Fixture fixture, string reason, Exception ex)
	    {
		    _logger.Error($"Fixture processing errored due to \"{reason}\" {fixture.ToStringShort()} {ex}");
		    if (context != null)
		    {
			    context.Errored = DateTime.UtcNow;
			    context.Reason = reason;
		    }
	    }


		







    }
}

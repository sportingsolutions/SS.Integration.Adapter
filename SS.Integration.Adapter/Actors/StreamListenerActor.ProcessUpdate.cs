using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Exceptions;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Exceptions;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
	public partial class StreamListenerActor
	{
		private bool PushToPlugin(Fixture fixture, bool isFullSnapshot, bool hasEpochChanged,
			bool skipMarketRules = false)
		{
			info.PushToPlugin(fixture, isFullSnapshot);

			if (fixture == null || (fixture != null && string.IsNullOrWhiteSpace(fixture.Id)))
				throw new ArgumentException($"Received empty {ActionName(isFullSnapshot)} for {_resource}");

			if (!_fixtureValidation.IsSequnceActual(fixture, _currentSequence))
			{
				info.ProcessingCancelledSequnceOutdated(fixture);
				return false;
			}

			if (!ValidateFixtureTimeStamp(fixture, isFullSnapshot))
			{
				info.ProcessingCancelledTimeOutdated(fixture);
				HandleUpdateDelay(fixture);
				return false;
			}



			if (_fixtureIsSuspended && !isFullSnapshot)
			{
				info.PushToPluginCancelled(fixture, "fixture suspended, snapshot will be requested");
				return RetrieveAndProcessSnapshot();
			}

			try
			{
				AplyAndLogMarketRules(fixture, skipMarketRules);

				//Processing on plugin side
				ProcessPluginActions(fixture, isFullSnapshot, hasEpochChanged);
				UpdateFixtureState(fixture, isFullSnapshot);


				

				if (_fixtureIsSuspended)
					UnsuspendFixtureState(GetFixtureState());

				UpdateSupervisorState(fixture, isFullSnapshot);
			}
			catch (FixtureIgnoredException)
			{
				info.ProcessingCancelled(fixture, "received a FixtureIgnoredException");
				return false;
			}
			catch (AggregateException ex)
			{
				foreach (var e in ex.InnerExceptions)
				{
					info.ProcessingErrored(fixture, "plugin error", e);
				}
				_marketsRuleManager.RollbackChanges();
				throw;
			}
			catch (Exception ex)
			{
				info.ProcessingErrored(fixture, "plugin error", ex);
				_marketsRuleManager.RollbackChanges();
				throw;
			}
			return true;
		}




		private void ProcessPluginActions(Fixture fixture, bool isFullSnapshot, bool hasEpochChanged)
		{
			info.PluginProcessingStarted(fixture);
			try
			{
				if (isFullSnapshot)
				{
					_platformConnector.ProcessSnapshot(fixture, hasEpochChanged);
				}
				else
				{
					_platformConnector.ProcessStreamUpdate(fixture, hasEpochChanged);
				}
			}
			catch (Exception ex)
			{
				var pluginError = new PluginException($"Plugin {ActionName(isFullSnapshot)} {fixture} error occured", ex);
				info.PluginProcessingErrored(fixture, ex);
				UpdateStatsError(pluginError);
				throw pluginError;
			}
			info.PluginProcessingFinished(fixture);
		}

		private string ActionName(bool isFullSnapshot) => isFullSnapshot ? "Snapshot" : "Update";

		#region Validation
		

		

		private void HandleUpdateDelay(Fixture snapshot)
		{
			Context.System.Scheduler.ScheduleTellOnce(_settings.delayedFixtureRecoveryAttemptSchedule * 1000,
				Self, new RecoverDelayedFixtureMsg { Sequence = snapshot.Sequence }, Self);
			_logger.Info(
				$"{snapshot} is suspend{(_fixtureIsSuspended ? "ed" : "ing")}, due to delay unsuspend scheduled after timeInSeconds={_settings.delayedFixtureRecoveryAttemptSchedule}");
			if (!_fixtureIsSuspended)
				SuspendFixture(SuspensionReason.UPDTATE_DELAYED);
		}

		private bool ValidateFixtureTimeStamp(Fixture fixture, bool isFullSnapshot)
		{
			if (isFullSnapshot)
			{
				_logger.Info(
					$"Method=ValidateFixtureTimeStamp will be ignored for fixture,  fixtureId={_fixtureId}, sequence={fixture.Sequence}");
				return true;
			}

			if (fixture.TimeStamp == null)
			{
				_logger.Warn(
					$"ValidateFixtureTimeStamp failed for fixture  with  fixtureId={_fixtureId}, sequence={fixture.Sequence}, fixture.TimeStamp=null");
				return false;
			}

			var timeStamp = fixture.TimeStamp.Value;
			var now = DateTime.UtcNow;
			var delayInSeconds = (now - timeStamp).TotalSeconds;

			if (delayInSeconds >= _settings.maxFixtureUpdateDelayInSeconds)
			{
				_logger.Warn(
					$"ValidateFixtureTimeStamp failed for fixture  with  fixtureId={_fixtureId}, sequence={fixture.Sequence}, " +
					$"delay={delayInSeconds} sec");
				return false;
			}

			_logger.Debug(
				$"ValidateFixtureTimeStamp successfully passed for fixture  with  fixtureId={_fixtureId}, sequence={fixture.Sequence}, " +
				$"delay={delayInSeconds} sec");
			return true;
		}



		#endregion


		


		#region MarketRules
		private void AplyAndLogMarketRules(Fixture snapshot, bool skipMarketRules)
		{
			_logger.Info(
				$"BeforeMarketRules MarketsCount={snapshot.Markets.Count} ActiveMarketsCount={snapshot.Markets.Count(_ => _.IsActive)} SelectionsCount={snapshot.Markets.SelectMany(_ => _.Selections).Count()} {snapshot}");
			if (!skipMarketRules)
			{
				_marketsRuleManager.ApplyRules(snapshot);
				snapshot.IsModified = true;
			}
			else
			{
				_marketsRuleManager.ApplyRules(snapshot, true);
			}

			_logger.Info(
				$"AfterMarketRules MarketsCount={snapshot.Markets.Count} ActiveMarketsCount={snapshot.Markets.Count(_ => _.IsActive)} SelectionsCount={snapshot.Markets.SelectMany(_ => _.Selections).Count()} {snapshot}");
		} 
		#endregion


		
	}
}

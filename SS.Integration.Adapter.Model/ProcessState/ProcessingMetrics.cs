using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Common.Extensions;

namespace SS.Integration.Adapter.Model.ProcessState
{
	public class ProcessingMetrics
	{


		public int Sequence { get; set; }

		public DateTime PickupFromQueue { get; set; }
		public DateTime AdapterPickup { get; set; }
		public DateTime StreamUpdateMsgReceived { get; set; }


		public DateTime? MessageDeserialized { get; set; }




		public string Reason { get; set; }
		public DateTime? Cancelled { get; set; }

		
		public DateTime? Errored { get; set; }

		public DateTime? Completed { get; set; }

		//public FixtureActionMetrics Update { get; set; }
		//public FixtureActionMetrics Snapshot { get; set; }
		//public FixtureActionMetrics RetrieveredSnapshot { get; set; }
		//public FixtureActionMetrics Suspend { get; set; }
		//public FixtureActionMetrics Unsuspend { get; set; }


		public List<FixtureActionMetrics> Actions { get; set; } = new List<FixtureActionMetrics>();

		public FixtureActionMetrics CurrentAction => Actions[Actions.Count - 1];

		public override string ToString()
		{
			TimeSpan st = AdapterPickup - PickupFromQueue;

			var pf = Cancelled ?? Errored ?? Completed;
			var f =  pf ?? DateTime.UtcNow;
			TimeSpan at = StreamUpdateMsgReceived - AdapterPickup;
			TimeSpan? dt = null;
			string actions = "";
			string comment = "";
			var tt = f - PickupFromQueue;

			var prodTime = tt;
			var actionsWithPlugin = Actions.Where(_ => _.PluginTime.HasValue).ToList();
			if (actionsWithPlugin.Any())
			{
				var lastActionWithPlugin = actionsWithPlugin[actionsWithPlugin.Count - 1];
				if (lastActionWithPlugin.Finish.HasValue)
					prodTime = lastActionWithPlugin.Finish.Value - PickupFromQueue;
			}
			


			if (MessageDeserialized.HasValue)
			{
				dt = MessageDeserialized.Value - StreamUpdateMsgReceived;
				actions = $"actionsCount={Actions.Count} ( {string.Join(" , ", Actions)} )";
			}
			else
			{
				comment = comment + " DeserializationFailed";
			}

			if (!pf.HasValue)
				comment = comment + " ProcessingFinishUnpopulated";




			string result = "Unknown";
			if (Errored.HasValue)
				result = "Errored";
			if (Cancelled.HasValue)
				result = "Cancelled";
			if (Completed.HasValue)
				result = "Completed";

			return $"UpdateProcessingReport {tt.TimeString("Total")} {prodTime.TimeString("Productive")}  {st.TimeString("SDK")} {at.TimeString("Adapter")} {dt.TimeString("Deserialize")} {actions} result={result} {comment}";
		}
	}
}

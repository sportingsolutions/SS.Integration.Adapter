using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Common.Extensions;

namespace SS.Integration.Adapter.Model.ProcessState
{
	public class FixtureActionMetrics
	{
		public FixtureActionMetrics(string name, DateTime started)
		{
			Name = name;
			Started = started;
		}

		public string Name { get; private set; }
		public DateTime Started { get; private set; }
		public DateTime? PluginTime { get; set; }
		public DateTime? Cancelled { get; set; }
		public string Reason { get; set; }
		public DateTime? Errored { get; set; }
		public DateTime? Completed { get; set; }



		public override string ToString()
		{
			var f = Finish;
			TimeSpan? at = new TimeSpan();
			TimeSpan? pt = new TimeSpan();
			string comment = ReasonString;
			if (f.HasValue)
			{
				if (PluginTime.HasValue)
				{
					at = PluginTime.Value - Started;
					pt = f.Value - PluginTime.Value;
				}
				else
				{
					at = f.Value - Started;
				}
			}
			else
			{
				comment = $"{comment} ActionFinishUnpopulated";
			}

			string result = "Unknown";
			if (Errored.HasValue)
				result = "Errored";
			if (Cancelled.HasValue)
				result = "Cancelled";
			if (Completed.HasValue)
				result = "Completed";

			return $"{at.TimeString($"{Name}Adapter", true)} {pt.TimeString($"{Name}Plugin", true)} {Name}Result={result}{comment}";

		}

		public DateTime? Finish => Completed ?? Cancelled ?? Errored ;

		private string ReasonString => Reason != null ? $"{Name}Comment={Reason}" : "";
	}
}

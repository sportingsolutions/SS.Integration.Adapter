using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Helpers
{
	public static class PriorityHelper
	{
		public static void SortByMatchStatus(this List<IResourceFacade> resources)
		{
			resources.Sort((x, y) => { return CompareToByStatusAndDate(x, y); });
		}

		public static int CompareToByStatusAndDate(this IResourceFacade x, IResourceFacade y)
		{
            if (x.Content == null)
                return -1;

            if (y.Content == null)
                return 1;

			if (x.Content.MatchStatus == y.Content.MatchStatus)
				return CompareByStartDate(x, y);

			if (x.Content.MatchStatus == 40)
				return -1;

			if (y.Content.MatchStatus == 40)
				return 1;

			if (x.Content.MatchStatus == 30)
				return -1;

			if (y.Content.MatchStatus == 30)
				return 1;

			return (int) x.Content.MatchStatus.CompareTo((int) y.Content.MatchStatus);
		}

		private static int CompareByStartDate(IResourceFacade x, IResourceFacade y)
		{
            if (string.IsNullOrWhiteSpace(x.Content.StartTime))
                return -1;

            if (string.IsNullOrWhiteSpace(y.Content.StartTime))
                return 1;

            var xD = DateTime.Parse(x.Content.StartTime);
			var yD = DateTime.Parse(y.Content.StartTime);
			var date = DateTime.Now;
			if (xD.Date == date.Date && yD.Date != date.Date)
				return -1;

			if (xD.Date != date.Date && yD.Date == date.Date)
				return 1;

			return xD.CompareTo(yD);
		}
	}
}

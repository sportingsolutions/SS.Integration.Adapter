using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Common.Extensions
{
	public static class DecimalExtensions
	{
		public static decimal Round(this decimal v, int count = 3)
		{
			return decimal.Round(v, count, MidpointRounding.ToEven);

		}
	}
}

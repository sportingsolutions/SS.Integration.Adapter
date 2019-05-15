using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Model.Enums
{
    public static class EnumExtensions
    {
        public static bool IsMatchOver(this MatchStatus matchStatus)
        {
            return matchStatus == MatchStatus.MatchOver || matchStatus == MatchStatus.AbandonedPreMatch || matchStatus == MatchStatus.Abandoned;
        }
    }
}

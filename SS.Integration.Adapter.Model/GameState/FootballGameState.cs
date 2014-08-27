using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Model.GameState
{
    public class FootballGameState
    {
        private const string MATCH_SUMMARY_TAG = "matchsummary";

        public FootballGameState(Fixture fixture)
        {
            if(fixture == null)
                throw new ArgumentException("Fixture passed to Football gamestate was null");

            if (fixture.GameState == null)
                throw new ArgumentException(string.Format("{0} : Game state is null, can't create the Football Game state.",fixture));

            ParseGameState(fixture);
        }

        private void ParseGameState(Fixture fixture)
        {
            var gameState = fixture.GameState;

            if(!gameState.ContainsKey(MATCH_SUMMARY_TAG))
                throw new ArgumentException(string.Format("{0} ,Match summary is not present in gamestate.",fixture));

            var matchSummary = gameState[MATCH_SUMMARY_TAG].ToString();
            HomeGoals = int.Parse(Regex.Match(matchSummary, @"\d+").Value);
            AwayGoals = int.Parse(Regex.Match(matchSummary, @"-(\d+)").Groups[1].Value);
        }

        public int AwayGoals { get; protected set; }

        public int HomeGoals { get; protected set; }
        public int TotalGoals { get { return HomeGoals + AwayGoals; } }
    }
}

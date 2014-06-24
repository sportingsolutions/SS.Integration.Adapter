//Copyright 2014 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.Generic;

namespace SS.Integration.Adapter.Plugin.Model
{
    [Serializable]
    public class HandicapMapping 
    {
        private readonly bool _isOverUnder;
        private Dictionary<string, Score> _results;

        // for serializer
        public HandicapMapping()
        {

        }

        

        public HandicapMapping(double line, string homeId, string awayId, string drawId = null,bool isOverUnder = false)
        {
            _isOverUnder = isOverUnder;

            Line = line;

            _results = new Dictionary<string, Score>(3);
            _results[homeId] = CalculateResult(line, true);
            _results[awayId] = CalculateResult(line, false);
            
            if(drawId != null)
                _results[drawId] = CalculateResult((int)line, null);
        }

        private bool IsLineFractional(double line)
        {
            var integerLine = (int)Math.Abs(line);
            return line > integerLine;
        }
        
        /// <param name="isHome">Null for draw</param>
        /// <returns></returns>
        private Score CalculateResult(double handicapLine, bool? isHome)
        {
            int line = (int)handicapLine;

            Score result;
            if (isHome.HasValue)
            {
                // home wins
                if (isHome.Value)
                {
                    // if it's 2.5 making it interger would make the score 2 which is insufficient
                    line = IsLineFractional(handicapLine) ? (int)++handicapLine : (int)handicapLine;
                    result = line > 0 ? new Score{ Home = line, Away = 0 } : new Score {Home = Math.Abs(line) + 1, Away = 0};
                }
                // away wins
                else
                {
                    // for over/under 2.5 under is considered away and needs to be decresed by one or there need to be another check in place so we don't increase it later
                    double awayLine = 0;

                    if (!IsLineFractional(handicapLine) && _isOverUnder)
                    {
                        awayLine = handicapLine - 1;
                        result = awayLine >= 0
                                     ? new Score {Home = 0, Away = (int) awayLine}
                                     : new Score {Home = 0, Away = (int) Math.Abs(awayLine)};
                    }
                    else
                    {
                        awayLine = IsLineFractional(handicapLine) && _isOverUnder ? --handicapLine : (int) handicapLine;
                        result = line >= 0 ? new Score {Home = 0, Away = (int) (awayLine + 1)} : new Score {Home = 0, Away = Math.Abs(line) + 1};
                    }
                }
            }
            else
            {
                //draw
                result = line > 0 ? new Score { Home = 0, Away = line } : new Score { Home = Math.Abs(line), Away = 0 };
            }

            return result;
        }

        public double Line { get; private set; }
        
        public Score GetResult(string winningSelectionId)
        {
            return string.IsNullOrEmpty(winningSelectionId) || !_results.ContainsKey(winningSelectionId) ? null : _results[winningSelectionId];
        }
    }
}

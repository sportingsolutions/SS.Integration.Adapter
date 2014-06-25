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

using System.Linq;
using log4net;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules
{
    public class DeltaRule : IMarketRule
    {
        private const string NAME = "Delta_Rule";
        private readonly ILog _logger = LogManager.GetLogger(typeof(DeltaRule));


        private static DeltaRule _instance;

        private DeltaRule() { }


        public static DeltaRule Instance
        {
            get { lock (typeof(DeltaRule)) { return _instance ?? (_instance = new DeltaRule()); } }
        }

        public string Name
        {
            get { return NAME; }
        }

        public IMarketRuleResultIntent Apply(Fixture fixture, IMarketStateCollection oldState, IMarketStateCollection newState)
        {
            _logger.DebugFormat("Applying market rule={0} for {1}", Name, fixture);
            MarketRuleResultIntent intent = new MarketRuleResultIntent();

            // only apply delta rule on a full snapshot
            if (fixture.Tags == null || !fixture.Tags.Any() || oldState == null)
                return intent;

            foreach (var mkt in fixture.Markets)
            {
                if (oldState.HasMarket(mkt.Id))
                {
                    if (oldState[mkt.Id].IsEquivalentTo(mkt, true, true))
                    {
                        _logger.InfoFormat("market rule={0} => {1} of {2} is marked as removable as nothing has changed",
                            Name, mkt, fixture);
                        intent.MarkAsRemovable(mkt);
                    }
                }
            }

            _logger.DebugFormat("rule={0} successfully applied on {1}", Name, fixture);

            return intent;
        }

    }
}

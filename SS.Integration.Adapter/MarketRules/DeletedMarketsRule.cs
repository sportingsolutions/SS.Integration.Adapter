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
    public class DeletedMarketsRule : IMarketRule
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof (DeletedMarketsRule));
        private static DeletedMarketsRule _instance;
        private const string NAME = "Delete_Markets";

        public string Name
        {
            get { return NAME; }
        }

        public static DeletedMarketsRule Instance
        {
            get { lock (typeof(DeletedMarketsRule)) return _instance ?? (_instance = new DeletedMarketsRule()); }
        }

        public IMarketRuleResultIntent Apply(Fixture fixture, IMarketStateCollection oldState,
            IMarketStateCollection newState)
        {
            var result = new MarketRuleResultIntent();

            if (oldState == null)
                return result;
            
            var deletedMarkets =
                newState.Markets.Select(marketId => newState[marketId])
                    .Where(marketState => marketState.IsDeleted && oldState.HasMarket(marketState.Id) && !oldState[marketState.Id].IsDeleted)
                    .ToList();

            var newDeletedMarketState = deletedMarkets.Select(CreateSuspendedMarket).ToList();
            
            if (deletedMarkets.Any())
            {
                newDeletedMarketState.ForEach(m =>
                {
                    _logger.InfoFormat("Market {0} was deleted from {1} and it will be suspended", m, fixture);
                    result.AddMarket(m);
                });
            }


            return result;

        }

        private static Market CreateSuspendedMarket(IMarketState MarketState)
        {
            var market = new Market(MarketState.Id) {IsSuspended = true};

            if (MarketState.HasTag("line"))
            {
                market.AddOrUpdateTagValue("line", MarketState.GetTagValue("line"));
            }

            foreach (var stateSelection in MarketState.Selections)
                market.Selections.Add(new Selection { Id = stateSelection.Id, Tradable = false, Price = 0 });

            return market;
        }

    }
}

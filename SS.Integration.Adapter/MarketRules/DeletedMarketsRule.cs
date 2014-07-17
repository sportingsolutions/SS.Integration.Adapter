using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Common.Extensions;

namespace SS.Integration.Adapter.MarketRules
{
    public class DeletedMarketsRule : IMarketRule
    {
        private ILog _logger = LogManager.GetLogger(typeof (DeletedMarketsRule));
        private static DeletedMarketsRule _instance;

        public string Name
        {
            get { return this.GetType().Name; }
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

            var test = oldState["_3z0qZjBERuS8kLYiqhuESaDZDM"].IsDeleted;

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
            var market = new Market(MarketState.Id);

            market.IsSuspended = true;
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

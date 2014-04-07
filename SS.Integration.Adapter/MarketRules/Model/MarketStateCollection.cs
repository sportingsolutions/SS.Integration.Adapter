using System;
using System.Collections.Generic;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.MarketRules.Model
{
    internal class MarketStateCollection : IMarketStateCollection
    {
        private readonly Dictionary<string, IMarketState> _States;

        public MarketStateCollection()
        {
            _States = new Dictionary<string, IMarketState>();
        }

        public MarketStateCollection(IMarketStateCollection collection) 
            : this()
        {
            foreach (var mkt_id in collection.Markets)
            {
                this[mkt_id] = collection[mkt_id].Clone();
            }
        }

        public bool HasMarket(string MarketId)
        {
            return _States.ContainsKey(MarketId);
        }

        public IMarketState this[string MarketId]
        {
            get
            {
                if (string.IsNullOrEmpty(MarketId))
                    throw new ArgumentNullException("MarketId");

                return HasMarket(MarketId) ? _States[MarketId] : null;
            }
            set
            {
                _States[MarketId] = value;
            }
        }

        public IEnumerable<string> Markets
        {
            get { return _States.Keys; }
        }

        public void Update(Fixture Fixture, bool fullSnapshot)
        {
            foreach (var market in Fixture.Markets)
            {
                IMarketState mkt_state = null;
                if (HasMarket(market.Id))
                {
                    mkt_state = this[market.Id];
                    mkt_state.Update(market, fullSnapshot);
                }
                else
                {
                    mkt_state = new MarketState(market, fullSnapshot);
                    this[market.Id] = mkt_state;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
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
    }
}

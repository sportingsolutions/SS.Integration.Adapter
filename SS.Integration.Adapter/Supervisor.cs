using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using log4net;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter
{
    public class Supervisor : StreamListenerManager, ISupervisor
    {
        private readonly Action<Dictionary<string,FixtureOverview>> _publishAction;
        private ILog _logger = LogManager.GetLogger(typeof (Supervisor));
        //private Subject<FixtureOverview> _fixtureEvents;
        private ConcurrentDictionary<string, FixtureOverview> _fixtures;
        private IDisposable _publisher;

        public Supervisor(ISettings settings) : base(settings)
        {
            //_publishAction = publishAction;
            //_publisher = Observable.Buffer(_fixtureEvents, TimeSpan.FromSeconds(1), 10).Subscribe(x => _publishAction(x.ToDictionary(f => f.Id)));
        }
        
        public void AddFixture(Fixture fixture)
        {
            _logger.DebugFormat("Something worked...");

        }

        public void RemoveFixture(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public void UpdateFixture(Fixture fixture)
        {
            throw new NotImplementedException();
        }

        public void OnConnected(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public void OnErrored(string fixtureId, string message)
        {
            throw new NotImplementedException();
        }

        public void OnErrored(string fixtureId, Exception ex)
        {
            throw new NotImplementedException();
        }
    }
}

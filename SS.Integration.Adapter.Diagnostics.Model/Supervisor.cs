using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using log4net;
using SS.Integration.Adapter.Diagnostics.Interface;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Diagnostics
{
    public class Supervisor : ISupervisor
    {
        private readonly Action<FixtureOverview> _publishAction;
        private ILog _logger = LogManager.GetLogger(typeof (Supervisor));

        public Supervisor(Action<FixtureOverview> publishAction)
        {
            _publishAction = publishAction;
        }

        private ConcurrentDictionary<string, FixtureOverview> _fixtures;

        public void AddFixture(Fixture fixture)
        {
            _publishAction(new FixtureOverview() {Id = fixture.Id});
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
        
    }
}

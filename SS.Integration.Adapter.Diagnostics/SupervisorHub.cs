using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Diagnostics.Host
{
    //[HubName("SupervisorHub")]
    public class SupervisorHub : Hub
    {
        public void Publish(FixtureOverview fixtureOverview)
        {
            Clients.All.publish(fixtureOverview);
        }
    }
}

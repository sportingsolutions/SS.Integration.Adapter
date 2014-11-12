using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Interface
{
    public interface ISupervisor : IStreamListenerManager
    {
        void ForceSnapshot(string fixtureId);
        IObservable<FixtureOverview> GetFixtureOverviewStream();
    }
}

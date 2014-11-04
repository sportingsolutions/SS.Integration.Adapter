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
        void AddFixture(Fixture fixture);
        void RemoveFixture(string fixtureId);

        void UpdateFixture(Fixture fixture);

        void OnConnected(string fixtureId);
        void OnErrored(string fixtureId, string message);
        void OnErrored(string fixtureId, Exception ex);




    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SportingSolutions.Udapi.Sdk;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter
{
    public class StreamListenerManager : IStreamListenerManager
    {
        public bool HasStreamListener(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public void StartStreaming(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public void StopStreaming(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public bool RemoveStreamListener(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public bool AddStreamListener(Resource resource)
        {
            throw new NotImplementedException();
        }
    }
}

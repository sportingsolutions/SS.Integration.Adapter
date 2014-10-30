using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SportingSolutions.Udapi.Sdk;

namespace SS.Integration.Adapter.Interface
{
    interface IStreamListenerManager
    {
        bool HasStreamListener(string fixtureId);
        void StartStreaming(string fixtureId);
        void StopStreaming(string fixtureId);
        bool RemoveStreamListener(string fixtureId);
        bool AddStreamListener(Resource resource);
    }
}

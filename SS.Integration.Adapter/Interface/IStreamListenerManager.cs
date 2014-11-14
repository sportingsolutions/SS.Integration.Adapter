using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SportingSolutions.Udapi.Sdk;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Interface
{
    public interface IStreamListenerManager
    {
        bool HasStreamListener(string fixtureId);
        void StartStreaming(string fixtureId);
        void StopStreaming(string fixtureId);
        bool RemoveStreamListener(string fixtureId);
        bool AddStreamListener(Resource resource);

        int Count { get; }

        void StopAll();

        event Adapter.StreamEventHandler StreamCreated;
        event Adapter.StreamEventHandler StreamRemoved;
        void RemoveDeletedFixtures(string sport, Dictionary<string, IResourceFacade> currentfixturesLookup);
        bool RemoveAndStopListener(string fixtureId);
        IEnumerable<IGrouping<string, IListener>> GetListenersBySport();
        bool ProcessResource(IResourceFacade resource);
        void CreateStreamListener(IResourceFacade resource, IStateManager stateManager, IAdapterPlugin platformConnector);
        bool RemoveStreamListenerIfFinishedProcessing(IResourceFacade resource);
        bool CanBeProcessed(string fixtureId);
    }
}

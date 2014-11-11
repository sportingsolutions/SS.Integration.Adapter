using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using log4net;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter
{
    public class Supervisor : StreamListenerManager, ISupervisor
    {
        private readonly Action<Dictionary<string, FixtureOverview>> _publishAction;
        private ILog _logger = LogManager.GetLogger(typeof(Supervisor));
        
        private ConcurrentDictionary<string, FixtureOverview> _fixtures;
        private IDisposable _publisher;

        public Supervisor(ISettings settings)
            : base(settings)
        {
            //_publishAction = publishAction;
            //_publisher = Observable.Buffer(_fixtureEvents, TimeSpan.FromSeconds(1), 10).Subscribe(x => _publishAction(x.ToDictionary(f => f.Id)));
        }

        public override void CreateStreamListener(IResourceFacade resource, IStateManager stateManager, IAdapterPlugin platformConnector)
        {
            base.CreateStreamListener(resource,stateManager,platformConnector);
            var listener = GetStreamListener(resource.Id);

            var streamListener = listener as StreamListener;
            if (streamListener != null)
            {
                streamListener.OnConnected += StreamListenerConnected;
                streamListener.OnDisconnected += StreamListenerDisconnected;
                streamListener.OnError += StreamListenerErrored;
                streamListener.OnFlagsChanged += StreamListenerFlagsChanged;
                streamListener.OnSnapshot += StreamListenerSnapshot;
                streamListener.OnStreamUpdate += StreamListenerStreamUpdate;
                streamListener.OnSuspend += StreamListenerSuspended;
                streamListener.OnStop += StreamListenerStop;
            }
        }

        public override void StopStreaming(string fixtureId)
        {
            var listener = GetStreamListener(fixtureId);
            base.StopStreaming(fixtureId);

            var streamListener = listener as StreamListener;
            if(streamListener == null) return;

            streamListener.OnConnected      -= StreamListenerConnected;
            streamListener.OnError          -= StreamListenerErrored;
            streamListener.OnFlagsChanged   -= StreamListenerFlagsChanged;
            streamListener.OnSnapshot       -= StreamListenerSnapshot;
            streamListener.OnStreamUpdate   -= StreamListenerStreamUpdate;
            streamListener.OnSuspend        -= StreamListenerSuspended;
            streamListener.OnStop           -= StreamListenerStop;
            streamListener.OnDisconnected   -= StreamListenerDisconnected;
        }

        private void StreamListenerStop(object sender, StreamListenerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void StreamListenerSuspended(object sender, StreamListenerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void StreamListenerStreamUpdate(object sender, StreamListenerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void StreamListenerSnapshot(object sender, StreamListenerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void StreamListenerFlagsChanged(object sender, StreamListenerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void StreamListenerErrored(object sender, StreamListenerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void StreamListenerDisconnected(object sender, StreamListenerEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void StreamListenerConnected(object sender, StreamListenerEventArgs e)
        {
            throw new NotImplementedException();
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

        public void OnError(string fixtureId, Exception ex)
        {
            throw new NotImplementedException();
        }

        public override void StartStreaming(string fixtureId)
        {
            base.StartStreaming(fixtureId);
            UpdatePropertiesBasedOnStreamListener(fixtureId);

        }

        private void UpdatePropertiesBasedOnStreamListener(string fixtureId)
        {
            var listener = GetStreamListener(fixtureId);
            
            //Nothing to update
            if (listener == null)
                return;

            //this is accessing a dictionary object
            var fixtureOverview = GetFixtureOverview(fixtureId);

            fixtureOverview.Id = fixtureId;
            fixtureOverview.IsDeleted = listener.IsFixtureDeleted;
            fixtureOverview.IsStreaming = listener.IsStreaming;
            fixtureOverview.IsOver = listener.IsFixtureEnded;

            var streamListener = listener as StreamListener;
            
            //this should be only null in unit tests
            if(streamListener == null)
                return;

            fixtureOverview.IsErrored = streamListener.IsErrored;
        }

        private FixtureOverview GetFixtureOverview(string fixtureId)
        {
            return _fixtures.ContainsKey(fixtureId) ? _fixtures[fixtureId] : _fixtures[fixtureId] = new FixtureOverview();
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
        private Subject<FixtureOverview> _changeTracker = new Subject<FixtureOverview>();
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
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerSuspended(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId);
            fixtureOverview.IsSuspended = true;
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerStreamUpdate(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId);

            //assumption
            fixtureOverview.IsSuspended = false;
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerSnapshot(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId);
            
            //assumption
            fixtureOverview.IsSuspended = false;
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerFlagsChanged(object sender, StreamListenerEventArgs e)
        {
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerErrored(object sender, StreamListenerEventArgs e)
        {
            UpdateStateFromEventDetails(e);
        }
        
        private void StreamListenerDisconnected(object sender, StreamListenerEventArgs e)
        {
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerConnected(object sender, StreamListenerEventArgs e)
        {
            UpdateStateFromEventDetails(e);
        }
        
        public void ForceSnapshot(string fixtureId)
        {
            var listener = GetStreamListener(fixtureId) as StreamListener;
            if(listener == null)
                throw new NullReferenceException("Can't convert Listener to StreamListener in ForceSnapshot method");
            
            //skips market rules
            listener.RetrieveAndProcessSnapshot(false,true);
        }

        public override void StartStreaming(string fixtureId)
        {
            base.StartStreaming(fixtureId);
            UpdateStateFromStreamListener(fixtureId);

        }

        private void UpdateStateFromStreamListener(string fixtureId)
        {
            var listener = GetStreamListener(fixtureId);
            UpdateStateFromStreamListener(listener as StreamListener);
        }

        private void UpdateStateFromEventDetails(StreamListenerEventArgs streamListenerEventArgs)
        {
            UpdateStateFromStreamListener(streamListenerEventArgs.Listener as StreamListener);
            var fixtureOverview = GetFixtureOverview(streamListenerEventArgs.Listener.FixtureId);
            fixtureOverview.Sequence = streamListenerEventArgs.CurrentSequence;
            fixtureOverview.Epoch = streamListenerEventArgs.Epoch;
            
            if (streamListenerEventArgs.Exception != null)
                fixtureOverview.LastError = streamListenerEventArgs.Exception;

            _changeTracker.OnNext(fixtureOverview);
        }

        private void UpdateStateFromStreamListener(StreamListener listener)
        {
            //Nothing to update
            if (listener == null)
                return;

            //this is accessing a dictionary object
            var fixtureOverview = GetFixtureOverview(listener.FixtureId);

            fixtureOverview.Id = listener.FixtureId;
            fixtureOverview.IsDeleted = listener.IsFixtureDeleted;
            fixtureOverview.IsStreaming = listener.IsStreaming;
            fixtureOverview.IsOver = listener.IsFixtureEnded;
            fixtureOverview.IsErrored = listener.IsErrored;
        }

        
        public IObservable<FixtureOverview> GetFixtureOverviewStream()
        {
            return _changeTracker;
        }

        private FixtureOverview GetFixtureOverview(string fixtureId)
        {
            return _fixtures.ContainsKey(fixtureId) ? _fixtures[fixtureId] : _fixtures[fixtureId] = new FixtureOverview();
        }

        private StreamListener GetStreamListenerObject(string fixtureId)
        {
            return GetStreamListener(fixtureId) as StreamListener;
        }

    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Subjects;
using log4net;
using SS.Integration.Adapter.Diagnostics.Model;
using SS.Integration.Adapter.Diagnostics.Model.Interface;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Diagnostics.RestService;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Diagnostics
{
    public class Supervisor : StreamListenerManager, ISupervisor
    {
        private ILog _logger = LogManager.GetLogger(typeof(Supervisor));

        private readonly ConcurrentDictionary<string, FixtureOverview> _fixtures;
        private readonly Subject<IFixtureOverviewDelta> _changeTracker = new Subject<IFixtureOverviewDelta>();
        private IDisposable _publisher;

        public Supervisor(ISettings settings)
            : base(settings)
        {
            _fixtures = new ConcurrentDictionary<string, FixtureOverview>();
            Proxy = new SupervisorProxy(this);
        }

        public void Initialise()
        {
            // TODO REST service should be configurable
            Service = new Service(Proxy);
            Service.Start();
        }

        public ISupervisorProxy Proxy { get; private set; }

        public ISupervisorService Service { get; private set; }

        public override void CreateStreamListener(IResourceFacade resource, IStateManager stateManager, IAdapterPlugin platformConnector)
        {
            base.CreateStreamListener(resource, stateManager, platformConnector);
            var listener = GetStreamListener(resource.Id);

            var streamListener = listener as StreamListener;
            if (streamListener != null)
            {
                //OnConnected event will not be called the first time StreamListener connects if it's already in Prematch/InPlay 
                //because connection in this case will occur before the event was connected
                streamListener.OnConnected += StreamListenerConnected;

                streamListener.OnDisconnected += StreamListenerDisconnected;
                streamListener.OnError += StreamListenerErrored;
                streamListener.OnFlagsChanged += StreamListenerFlagsChanged;
                streamListener.OnBeginSnapshotProcessing += StreamListenerSnapshot;
                streamListener.OnFinishedSnapshotProcessing += StreamListenerFinishedProcessingUpdate;
                streamListener.OnBeginStreamUpdateProcessing += StreamListenerBeginStreamUpdate;
                streamListener.OnFinishedStreamUpdateProcessing += StreamListenerFinishedProcessingUpdate;
                streamListener.OnSuspend += StreamListenerSuspended;
                streamListener.OnStop += StreamListenerStop;
            }

            UpdateStateFromStreamListener(streamListener);
            var fixtureOverview = GetFixtureOverview(listener.FixtureId) as FixtureOverview;
            _changeTracker.OnNext(fixtureOverview.GetDelta());
        }
        

        public override void StopStreaming(string fixtureId)
        {
            var listener = GetStreamListener(fixtureId);
            base.StopStreaming(fixtureId);

            var streamListener = listener as StreamListener;
            if (streamListener == null) return;

            streamListener.OnConnected -= StreamListenerConnected;
            streamListener.OnError -= StreamListenerErrored;
            streamListener.OnFlagsChanged -= StreamListenerFlagsChanged;
            streamListener.OnBeginSnapshotProcessing -= StreamListenerSnapshot;
            streamListener.OnBeginStreamUpdateProcessing -= StreamListenerBeginStreamUpdate;
            streamListener.OnFinishedStreamUpdateProcessing -= StreamListenerFinishedProcessingUpdate;
            streamListener.OnFinishedSnapshotProcessing -= StreamListenerFinishedProcessingUpdate;
            streamListener.OnSuspend -= StreamListenerSuspended;
            streamListener.OnStop -= StreamListenerStop;
            streamListener.OnDisconnected -= StreamListenerDisconnected;
        }

        private void StreamListenerFinishedProcessingUpdate(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId);

            if (fixtureOverview.FeedUpdate != null && fixtureOverview.FeedUpdate.Sequence == e.CurrentSequence)
            {
                var feedUpdate = fixtureOverview.FeedUpdate;
                feedUpdate.IsProcessed = true;
                feedUpdate.ProcessingTime = DateTime.UtcNow - feedUpdate.Issued;
            }

            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerBeginStreamUpdate(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId) as FixtureOverview;

            fixtureOverview.FeedUpdate = CreateFeedUpdate(e);

            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerStop(object sender, StreamListenerEventArgs e)
        {
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerSuspended(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId) as FixtureOverview;
            fixtureOverview.IsSuspended = true;
            UpdateStateFromEventDetails(e);
        }

        private FeedUpdateOverview CreateFeedUpdate(StreamListenerEventArgs streamListenerArgs, bool isSnapshot = false)
        {
            var feedUpdate = new FeedUpdateOverview
            {
                Issued = DateTime.UtcNow,
                Sequence = streamListenerArgs.CurrentSequence,
                IsSnapshot = isSnapshot
            };

            return feedUpdate;
        }

        private void StreamListenerSnapshot(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId) as FixtureOverview;

            //assumption
            fixtureOverview.IsSuspended = false;

            fixtureOverview.FeedUpdate = CreateFeedUpdate(e, true);
            
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerFlagsChanged(object sender, StreamListenerEventArgs e)
        {
            UpdateStateFromEventDetails(e);
        }

        private void StreamListenerErrored(object sender, StreamListenerEventArgs e)
        {
            var fixtureOverview = GetFixtureOverview(e.Listener.FixtureId) as FixtureOverview;
            fixtureOverview.LastError = new ErrorOverview
            {
                ErroredAt = DateTime.UtcNow,
                Exception = e.Exception,
                IsErrored = e.Listener.IsErrored,
                Sequence = e.CurrentSequence
            };
            
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
            if (listener == null)
                throw new NullReferenceException("Can't convert Listener to StreamListener in ForceSnapshot method");

            //skips market rules
            listener.RetrieveAndProcessSnapshot(false, true);
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
            
            var fixtureOverview = GetFixtureOverview(streamListenerEventArgs.Listener.FixtureId) as FixtureOverview;
            fixtureOverview.Sequence = streamListenerEventArgs.CurrentSequence;
            fixtureOverview.Epoch = streamListenerEventArgs.Epoch;
            fixtureOverview.StartTime = streamListenerEventArgs.StartTime;
            fixtureOverview.Name = streamListenerEventArgs.Name ?? fixtureOverview.Name;

            if (fixtureOverview.LastError != null
                && fixtureOverview.LastError.IsErrored
                && !streamListenerEventArgs.Listener.IsErrored)
            {
                fixtureOverview.LastError.ResolvedAt = DateTime.UtcNow;
                fixtureOverview.LastError.IsErrored = false;

                //this is to force delta update
                fixtureOverview.LastError = fixtureOverview.LastError;
            }         

            _changeTracker.OnNext(fixtureOverview.GetDelta());
        }

        private void UpdateStateFromStreamListener(StreamListener listener)
        {
            //Nothing to update
            if (listener == null)
                return;

            //this is accessing a dictionary object
            var fixtureOverview = GetFixtureOverview(listener.FixtureId) as FixtureOverview;

            fixtureOverview.Id = listener.FixtureId;
            fixtureOverview.Sport = listener.Sport;
            fixtureOverview.IsDeleted = listener.IsFixtureDeleted;
            fixtureOverview.IsStreaming = listener.IsStreaming;
            fixtureOverview.IsOver = listener.IsFixtureEnded;
            fixtureOverview.IsErrored = listener.IsErrored;
            fixtureOverview.TimeStamp = DateTime.UtcNow;
        }


        public IObservable<IFixtureOverviewDelta> GetFixtureOverviewStream()
        {
            return _changeTracker;
        }

        public IEnumerable<IFixtureOverview> GetFixtures()
        {
            return _fixtures.Values;
        }

        public IFixtureOverview GetFixtureOverview(string fixtureId)
        {
            return _fixtures.ContainsKey(fixtureId) ? _fixtures[fixtureId] : _fixtures[fixtureId] = new FixtureOverview();
        }

        private StreamListener GetStreamListenerObject(string fixtureId)
        {
            return GetStreamListener(fixtureId) as StreamListener;
        }

        #region IDisposable

        public void Dispose()
        {
            if (Service != null)
                Service.Stop();
        }

        #endregion
    }
}

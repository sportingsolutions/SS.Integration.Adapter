using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using SportingSolutions.Udapi.Sdk;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter
{
    public class StreamListenerManager : IStreamListenerManager
    {
        private readonly ILog _logger = LogManager.GetLogger(typeof (StreamListenerManager));

        private readonly ConcurrentDictionary<string, IListener> _listeners;
        private readonly ConcurrentDictionary<string, int> _listenerDisposingQueue;

        public event Adapter.StreamEventHandler StreamCreated;
        public event Adapter.StreamEventHandler StreamRemoved;
        public void RemoveDeletedFixtures(string sport, Dictionary<string, IResourceFacade> currentfixturesLookup)
        {
            var allFixturesForSport = _listeners.Where(x => string.Equals(x.Value.Sport, sport, StringComparison.Ordinal));

            // deletedFixtures = allFixturesForSport \ resources
            var deletedFixtures = allFixturesForSport.Where(fixture => !currentfixturesLookup.ContainsKey(fixture.Key));

            // existingFixtures = resources ^ allFixturesForSport
            var existingFixtures = allFixturesForSport.Where(fixture => currentfixturesLookup.ContainsKey(fixture.Key));


            foreach (var fixture in deletedFixtures)
            {
                if (_listenerDisposingQueue.ContainsKey(fixture.Key))
                {
                    if (_listenerDisposingQueue[fixture.Key] >= LISTENER_DISPOSING_SAFE_GUARD)
                    {

                        _logger.DebugFormat("Fixture with fixtureId={0} was deleted from Connect fixture factory", fixture.Key);
                        RemoveAndStopListener(fixture.Key);
                        EventState.RemoveFixture(sport, fixture.Key);
                    }
                    else
                    {
                        _listenerDisposingQueue[fixture.Key] = _listenerDisposingQueue[fixture.Key] + 1;
                    }
                }
                else
                {
                    _listenerDisposingQueue.TryAdd(fixture.Key, 1);
                    _logger.DebugFormat("Fixture with fixtureId={0} has been added to the disposing queue", fixture.Key);
                }
            }

            foreach (var fixture in existingFixtures)
            {
                if (_listenerDisposingQueue.ContainsKey(fixture.Key))
                {
                    int dummy;
                    _listenerDisposingQueue.TryRemove(fixture.Key, out dummy);
                    _logger.DebugFormat("Fixture with fixtureId={0} was marked as deleted, but it appered on Connect again", fixture.Key);
                }
            }
        }

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

        public int Count { get { return _listeners.Count; } }

        public void StopAll()
        {
            if (_listeners != null)
            {
                try
                {
                    DisposeListeners();
                }
                catch (AggregateException ax)
                {
                    foreach (var exception in ax.InnerExceptions)
                    {
                        _logger.Error("Error during listener disposing", exception);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error("Error during listener disposing", e);
                }

                _listeners.Clear();
            }
        }

        private void DisposeListeners()
        {
            _logger.Debug("Stopping all listeners and suspending all fixtures as requested");

            Parallel.ForEach(
                _listeners.Values,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                listener => listener.Dispose());
        }

        
    }
}

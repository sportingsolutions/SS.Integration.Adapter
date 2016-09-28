using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using log4net.Core;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter
{
    public class FixtureManager : IDisposable
    {

        private readonly BlockingCollection<IResourceFacade> _resourceCreationQueue = new BlockingCollection<IResourceFacade>(new ConcurrentQueue<IResourceFacade>());
        private readonly CancellationTokenSource _creationQueueCancellationToken = new CancellationTokenSource();
        private readonly Task[] _creationTasks;
        private readonly ILog _logger = LogManager.GetLogger(typeof(FixtureManager));
        private ConcurrentDictionary<string, IResourceFacade> _cachedResources = new ConcurrentDictionary<string, IResourceFacade>();

        internal IStreamListenerManager _streamManager { get; private set; }
        private Func<string, List<IResourceFacade>> GetResourcesForSportFunc { get; set; }

        public FixtureManager(int concurrencyLevel, IStreamListenerManager streamManager, Func<string, List<IResourceFacade>> getResourcesForSport)
        {
            GetResourcesForSportFunc = getResourcesForSport;
            _streamManager = streamManager;
            _streamManager.ProcessResourceHook = ReProcessFixture;

            _creationTasks = new Task[concurrencyLevel];
            for (var i = 0; i < concurrencyLevel; i++)
            {
                _creationTasks[i] = Task.Factory.StartNew(CreateFixture, _creationQueueCancellationToken.Token);
            }
        }

        private void CreateFixture()
        {
            try
            {
                foreach (var resource in _resourceCreationQueue.GetConsumingEnumerable(_creationQueueCancellationToken.Token))
                {
                    try
                    {
                        _logger.DebugFormat("Task={0} is processing {1} from the queue", Task.CurrentId, resource);

                        if (_streamManager.HasStreamListener(resource.Id))
                            continue;

                        ValidateCache(resource);
                        _streamManager.CreateStreamListener(resource, Adapter.PlatformConnector);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(string.Format("There has been a problem creating a listener for {0}", resource), ex);
                    }

                    if (_creationQueueCancellationToken.IsCancellationRequested)
                    {
                        _logger.DebugFormat("Fixture creation task={0} will terminate as requested", Task.CurrentId);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.DebugFormat("Fixture creation task={0} exited as requested", Task.CurrentId);
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("An error occured on fixture creation task={0}", Task.CurrentId), ex);
            }

        }

        private void ValidateCache(IResourceFacade resource)
        {
            //this ensures that cached object is not already used in stream listener - if we got a newer version is best to replace it
            if (_cachedResources.ContainsKey(resource.Id))
            {
                IResourceFacade ignore = null;
                _cachedResources.TryRemove(resource.Id, out ignore);
            }

        }


        public int QueueSize { get { return _resourceCreationQueue.Count; } }

        public void ReProcessFixture(string fixtureId)
        {
            if (!_cachedResources.ContainsKey(fixtureId))
            {
                _logger.WarnFormat("Attempted to fast track fixtureId={0} to creation queue but the resource did not exist. The fixture will be added on the next timer tick",fixtureId);
                return;
            }

            var resource = _cachedResources[fixtureId];
            ProcessResource(resource.Sport,resource);
        }

        public void StopProcessing()
        {
            _streamManager.ProcessResourceHook = null;
            _creationQueueCancellationToken.Cancel(false);
            Task.WaitAll(_creationTasks);

            _streamManager.StopAll();
        }

        public void ProcessSports(List<string> sports)
        {
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
            Parallel.ForEach(sports, parallelOptions, ProcessSport);
        }

        private bool ValidateResources(IList<IResourceFacade> resources, string sport)
        {
            var valid = true;

            if (resources == null)
            {
                _logger.WarnFormat("Cannot find sport={0} in UDAPI....", sport);
                valid = false;
            }
            else if (resources.Count == 0)
            {
                _logger.DebugFormat("There are currently no fixtures for sport={0} in UDAPI", sport);
                valid = false;
            }

            return valid;
        }

        //it's internal due to UT
        internal void ProcessSport(string sport)
        {
            _logger.InfoFormat("Getting the list of available fixtures for sport={0} from GTP", sport);

            var resources = GetResourcesForSportFunc(sport);
            
            if (ValidateResources(resources, sport))
            {
                var processingFactor = resources.Count / 10;

                _logger.DebugFormat("Received count={0} fixtures to process in sport={1}", resources.Count, sport);

                var po = new ParallelOptions { MaxDegreeOfParallelism = processingFactor == 0 ? 1 : processingFactor };

                if (resources.Count > 1)
                {
                    resources.Sort((x, y) =>
                    {
                        if (x.Content.MatchStatus > y.Content.MatchStatus)
                            return -1;

                        return x.Content.MatchStatus < y.Content.MatchStatus
                            ? 1
                            : DateTime.Parse(x.Content.StartTime).CompareTo(DateTime.Parse(y.Content.StartTime));

                    });
                }

                Parallel.ForEach(resources,
                    po,
                    resource =>
                    {
                        try
                        {
                            ProcessResource(sport, resource);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                string.Format("An error occured while processing {0} for sport={1}", resource, sport), ex);
                        }
                    });

            }
            
            if(resources != null)
                RemoveDeletedFixtures(sport, resources);

            _logger.InfoFormat("Finished processing fixtures for sport={0}", sport);
        }

        private void RemoveDeletedFixtures(string sport, IEnumerable<IResourceFacade> resources)
        {
            var currentfixturesLookup = resources.ToDictionary(r => r.Id);

            _streamManager.UpdateCurrentlyAvailableFixtures(sport, currentfixturesLookup);
        }

        private void ProcessResource(string sport, IResourceFacade resource)
        {
            _logger.DebugFormat("Attempt to process {0} for sport={1}", resource, sport);
            
            _cachedResources.AddOrUpdate(resource.Id, resource, (k, v) => v);
            
            // make sure that the resource is not already being processed by some other thread
            if (!_streamManager.CanBeProcessed(resource.Id))
                return;

            _logger.InfoFormat("Processing {0}", resource);

            if (_streamManager.ShouldProcessResource(resource) && _resourceCreationQueue.All(x => x.Id != resource.Id))
            {
                _logger.DebugFormat("Adding {0} to the creation queue ", resource);
                _resourceCreationQueue.Add(resource);
                _logger.DebugFormat("Added {0} to the creation queue", resource);
            }

        }
        public void Dispose()
        {
            _resourceCreationQueue.Dispose();
            _creationQueueCancellationToken.Dispose();
        }
    }
}

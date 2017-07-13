using System;
using System.Collections.Generic;
using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class has the responibility to process all the sports in parallel using different instances/threads managed by AKKA Router 
    /// A single sport is processed on a separate thread by a single instance acting like a child actor.
    /// The child actor is responsible for triggering the creation/processing of that sport's resources stream listeners to the StreamListenerManagerActor
    /// </summary>
    public class SportProcessorRouterActor : ReceiveActor
    {
        #region Constants

        public const string ActorName = nameof(SportProcessorRouterActor);
        public const string Path = "/user/" + ActorName;

        #endregion

        #region Attributes

        private readonly ILog _logger = LogManager.GetLogger(typeof(SportProcessorRouterActor));
        private readonly IServiceFacade _serviceFacade;

        #endregion

        #region Constructors

        public SportProcessorRouterActor(IServiceFacade serviceFacade)
        {
            _serviceFacade = serviceFacade ?? throw new ArgumentNullException(nameof(serviceFacade));

            Receive<ProcessSportMsg>(o => ProcessSportMsgHandler(o));
        }

        #endregion

        #region Private methods

        private void ProcessSportMsgHandler(ProcessSportMsg msg)
        {
            var resources = _serviceFacade.GetResources(msg.Sport);
            if (ValidateResources(resources, msg.Sport))
            {
                _logger.Debug($"Received {resources.Count} fixtures to process in sport={msg.Sport}");

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

                var streamListenerManagerActor = Context.System.ActorSelection(StreamListenerManagerActor.Path);

                foreach (var resource in resources)
                {
                    streamListenerManagerActor.Tell(new ProcessResourceMsg { Resource = resource }, Self);
                }
            }
        }

        private bool ValidateResources(IList<IResourceFacade> resources, string sport)
        {
            var valid = true;

            if (resources == null)
            {
                _logger.Warn($"Cannot find sport={sport} in UDAPI....");
                valid = false;
            }
            else if (resources.Count == 0)
            {
                _logger.Debug($"There are currently no fixtures for sport={sport} in UDAPI");
                valid = false;
            }

            return valid;
        }

        #endregion
    }
}

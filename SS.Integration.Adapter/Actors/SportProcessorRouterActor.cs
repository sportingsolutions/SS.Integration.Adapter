﻿using System;
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

        #endregion

        #region Attributes

        private readonly ILog _logger = LogManager.GetLogger(typeof(SportProcessorRouterActor));
        private readonly ISettings _settings;
        private readonly IAdapterPlugin _adapterPlugin;
        private readonly IStateManager _stateManager;
        private readonly IServiceFacade _serviceFacade;

        #endregion

        #region Constructors

        public SportProcessorRouterActor(
            ISettings settings, 
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager, 
            IServiceFacade serviceFacade)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _adapterPlugin = adapterPlugin ?? throw new ArgumentNullException(nameof(adapterPlugin));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
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
                _logger.DebugFormat(
                    $"Received {resources.Count} fixtures to process in sport={msg.Sport}");

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

                var streamListenerManagerActor =
                    Context.Child(StreamListenerManagerActor.ActorName + "For" + msg.Sport);
                if (streamListenerManagerActor.IsNobody())
                {
                    streamListenerManagerActor = Context.ActorOf(Props.Create(() =>
                            new StreamListenerManagerActor(
                                _settings,
                                _adapterPlugin,
                                _stateManager)),
                        StreamListenerManagerActor.ActorName + "For" + msg.Sport);
                }

                foreach (var resource in resources)
                {
                    streamListenerManagerActor.Tell(new CreateStreamListenerMsg { Resource = resource }, Self);
                }
            }
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

        #endregion
    }
}

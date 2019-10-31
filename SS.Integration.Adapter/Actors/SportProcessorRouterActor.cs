//Copyright 2017 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using log4net;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Helpers;
using SS.Integration.Adapter.Interface;

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

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(SportProcessorRouterActor));
        private readonly IServiceFacade _serviceFacade;
	    
		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="serviceFacade"></param>
		public SportProcessorRouterActor(IServiceFacade serviceFacade)
        {
            _serviceFacade = serviceFacade ?? throw new ArgumentNullException(nameof(serviceFacade));

            Receive<ProcessSportMsg>(o => ProcessSportsMsgHandler(o));
        }

        #endregion

        #region Protected methods

        protected override void PreRestart(Exception reason, object message)
        {
            _logger.Error(
                $"Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));
            base.PreRestart(reason, message);
        }

        #endregion

        #region Private methods

        

        private void ProcessSportsMsgHandler(ProcessSportMsg msg)
        {
            var sports = _serviceFacade.GetSports();
            if (sports == null)
            {
                var errorMsg = "ServiceFacade GetSports responce=NULL probably credentials problem";
                _logger.Error(errorMsg);
                throw new Exception(errorMsg);
            }

            _logger.Debug($"ServiceFacade GetSports returned SportsCount={sports?.Count()} listOfSports={$"\"{string.Join(", ", sports.Select(_=> _.Name))}\"" }");

            List <IResourceFacade> resources = new List<IResourceFacade>();
            foreach (var sport in sports)
            {
                var _res = _serviceFacade.GetResources(sport.Name);
                if(_res != null && _res.Any())
                    _logger.Info($"ProcessSportMsgHandler {GetListOfFixtures(_res, sport)}");
                if (ValidateResources(_res, sport.Name))
                {
                    resources.AddRange(_res);
                }
            }
            if (resources.Count > 1)
            {
                try
                {
                    resources.SortByMatchStatus();
                }
                catch (System.ArgumentException argEx)
                {
                    _logger.Warn($"Can't sort resources. Fixtures list: {GetListOfFixtures(resources)}. {argEx}");
                }
            }
            _logger.Info($"ProcessSportsMsgHandler resourcesCount={resources.Count}");

            var streamListenerManagerActor = Context.System.ActorSelection(StreamListenerManagerActor.Path);

            foreach (var resource in resources)
            {
                streamListenerManagerActor.Tell(new ProcessResourceMsg { Resource = resource }, Self);
            }

        }

        private string GetListOfFixtures(List<IResourceFacade> _res, IFeature sport = null)
        {
            string result = string.Empty;
            var sportName = sport != null ? sport.Name : "Any";
            try
            {
                var list = _res.Select(_ => $"fixtureId={_?.Id} sport={_?.Sport} name=\"{_?.Name}\" matchStatus={_?.MatchStatus} startTime={_?.Content?.StartTime}");
                result = $"sport ={sportName} count ={ list.Count()} resources =\"{string.Join(" , ", list)} \"";
            }
            catch(Exception)
            {
                _logger.Warn("ProcessSportsMsgHandler can't read fixtures info");
            }
            return result;
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
                valid = false;
            }

            return valid;
        }

        #endregion
    }
}

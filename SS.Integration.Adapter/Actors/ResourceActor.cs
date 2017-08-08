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

using Akka.Actor;
using log4net;
using SS.Integration.Adapter.Interface;
using System;
using SportingSolutions.Udapi.Sdk.Events;
using SS.Integration.Adapter.Actors.Messages;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class is responsible for managing resource and streaming 
    /// </summary>
    public class ResourceActor : ReceiveActor
    {
        #region Constants

        public const string ActorName = nameof(ResourceActor);

        #endregion

        #region Private members

        private readonly ILog _logger = LogManager.GetLogger(typeof(ResourceActor).ToString());
        private readonly IResourceFacade _resource;
        private readonly string _fixtureId;
        private readonly IActorRef _streamListenerActor;

        #endregion

        #region Constructors

        public ResourceActor(IActorRef streamListenerActor, IResourceFacade resource)
        {
            _streamListenerActor = streamListenerActor ?? throw new ArgumentNullException(nameof(streamListenerActor));
            _resource = resource ?? throw new ArgumentNullException(nameof(resource));
            _fixtureId = _resource.Id;

            Receive<ResourceStartStreamingMsg>(o => ResourceStartStreamingMsgHandler(o));
            Receive<ResourceStopStreamingMsg>(o => ResourceStopStreamingMsgHandler(o));

            Initialize();
        }

        #endregion

        #region Events Handlers

        private void Resource_StreamConnected(object sender, EventArgs e)
        {
            _logger.Info($"{_resource} Stream Connected");
            _streamListenerActor.Tell(new StreamConnectedMsg { FixtureId = _fixtureId });
        }

        private void Resource_StreamDisconnected(object sender, EventArgs e)
        {
            _logger.Info($"{_resource} Stream Disconnected");
            _streamListenerActor.Tell(new StreamDisconnectedMsg { FixtureId = _fixtureId });
        }

        protected override void PostStop()
        {
            base.PostStop();

            _resource.StreamConnected -= Resource_StreamConnected;
            _resource.StreamDisconnected -= Resource_StreamDisconnected;
            _resource.StreamEvent -= Resource_StreamEvent;
        }

        #endregion

        #region Private methods

        private void Initialize()
        {
            _resource.StreamConnected += Resource_StreamConnected;
            _resource.StreamDisconnected += Resource_StreamDisconnected;
            _resource.StreamEvent += Resource_StreamEvent;
        }

        private void Resource_StreamEvent(object sender, StreamEventArgs e)
        {
            _streamListenerActor.Tell(new StreamUpdateMsg { Data = e.Update });
        }

        private void ResourceStartStreamingMsgHandler(ResourceStartStreamingMsg msg)
        {
            _resource.StartStreaming();
        }

        private void ResourceStopStreamingMsgHandler(ResourceStopStreamingMsg msg)
        {
            _logger.Info("Resource will Stop Streaming");

            //StopStreaming will trigger Resource_StreamDisconnected
            _resource.StopStreaming();
        }

        #endregion
    }
}

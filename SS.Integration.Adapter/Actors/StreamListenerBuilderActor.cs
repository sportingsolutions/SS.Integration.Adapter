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
using Akka.Actor;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// StreamListener Builder is responsible for mananging concurrent creation of stream listeners
    /// </summary>
    public class StreamListenerBuilderActor : ReceiveActor, IWithUnboundedStash
    {
        #region Constants

        public const string ActorName = nameof(StreamListenerBuilderActor);

        #endregion

        #region Private members

        private readonly ISettings _settings;
        private readonly IActorContext _streamListenerManagerActorContext;
        private readonly IAdapterPlugin _adapterPlugin;
        private readonly IStateManager _stateManager;
        private readonly IStreamHealthCheckValidation _streamHealthCheckValidation;
        private readonly IFixtureValidation _fixtureValidation;
        private static int _concurrentInitializations;

        #endregion

        #region Properties

        public IStash Stash { get; set; }

        #endregion

        #region Constructors

        public StreamListenerBuilderActor(
            ISettings settings,
            IActorContext streamListenerManagerActorContext,
            IAdapterPlugin adapterPlugin,
            IStateManager stateManager,
            IStreamHealthCheckValidation streamHealthCheckValidation,
            IFixtureValidation fixtureValidation)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _streamListenerManagerActorContext =
                streamListenerManagerActorContext ??
                throw new ArgumentNullException(nameof(streamListenerManagerActorContext));
            _adapterPlugin = adapterPlugin ?? throw new ArgumentNullException(nameof(adapterPlugin));
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _streamHealthCheckValidation = streamHealthCheckValidation ?? throw new ArgumentNullException(nameof(streamHealthCheckValidation));
            _fixtureValidation = fixtureValidation ?? throw new ArgumentNullException(nameof(fixtureValidation));

            Active();
        }

        #endregion

        #region Behaviors

        //In the active state StreamListeners can be created on demand
        private void Active()
        {
            Receive<CreateStreamListenerMsg>(o => CreateStreamListenerMsgHandler(o));
            Receive<BuildStreamListenerActorMsg>(o => BuildStreamListenerActorMsgHandler(o));
        }

        //In the busy state the maximum concurrency has been already used and creation needs to be postponed until later
        private void Busy()
        {
            //Stash messages until CreationCompleted/Failed message is received
            Receive<CreateStreamListenerMsg>(o =>
            {
                if (_concurrentInitializations > _settings.FixtureCreationConcurrency)
                {
                    Stash.Stash();
                }
                else
                {
                    Become(Active);
                    Stash.Unstash();
                }
            });
        }

        #endregion

        #region Private methods

        private void CreateStreamListenerMsgHandler(CreateStreamListenerMsg msg)
        {
            _concurrentInitializations++;

            if (_concurrentInitializations > _settings.FixtureCreationConcurrency)
            {
                Become(Busy);
            }
            else
            {
                Self.Tell(new BuildStreamListenerActorMsg { Resource = msg.Resource });
            }
        }

        private void BuildStreamListenerActorMsgHandler(BuildStreamListenerActorMsg msg)
        {
            try
            {
                var streamListenerActorName = StreamListenerActor.GetName(msg.Resource.Id);
                if (_streamListenerManagerActorContext.Child(streamListenerActorName).IsNobody())
                {
                    _streamListenerManagerActorContext.ActorOf(Props.Create(() =>
                            new StreamListenerActor(
                                msg.Resource,
                                _adapterPlugin,
                                _stateManager,
                                _settings,
                                _streamHealthCheckValidation,
                                _fixtureValidation)),
                        streamListenerActorName);

                    Context.Parent.Tell(new StreamListenerCreationCompletedMsg { Resource = msg.Resource });
                }
            }
            catch (Exception ex)
            {
                Context.Parent.Tell(new StreamListenerCreationFailedMsg { Resource = msg.Resource, Exception = ex });
            }
            finally
            {
                _concurrentInitializations--;
            }
        }

        #endregion

        #region Private messages

        private class BuildStreamListenerActorMsg
        {
            public IResourceFacade Resource { get; set; }
        }

        #endregion
    }
}

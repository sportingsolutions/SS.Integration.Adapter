using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using SportingSolutions.Udapi.Sdk.Interfaces;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// StreamListener Builder is responsible for mananging concurrent creation of stream listeners
    /// </summary>
    public class StreamListenerBuilderActor : ReceiveActor, IWithUnboundedStash
    {
        private readonly ISettings _settings;
        private static int _concurrentInitializations = 0;

        public IStash Stash { get; set; }

        public StreamListenerBuilderActor(ISettings settings)
        {
            _settings = settings;
            Active();
        }

        //In the active state StreamListeners can be created on demand
        private void Active()
        {
            Receive<CreateStreamListenerMessage>(o => CreateStreamListenerMessageHandler(o));
            //After the message has been sent to stream listener 
            //This actor will schedule message to itself with ValidateStreamListenerCreationMessage 
            //If after 10mins StreamListener hasn't been created 
            //Notify StreamLIstenerManager it failed
        }

        //In the busy state the maximum concurrency has been already used and creation 
        //needs to be postponed until later
        private void Busy()
        {
            //Stash messages until CreationCompleted/Failed message is received
            Receive<CreateStreamListenerMessage>(o =>
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

        private void CreateStreamListenerMessageHandler(CreateStreamListenerMessage o)
        {
            if (_concurrentInitializations > _settings.FixtureCreationConcurrency)
            {
                Become(Busy);
            }
        }
    }

    #region Public messages

    internal class CreateStreamListenerMessage
    {
        internal IResourceFacade Resource { get; set; }
    }

    internal class StreamListenerCreationCompleted
    {

    }

    internal class StreamListenerCreationFailed
    {
        private string FixtureId { get; set; }
    }

    #endregion

}

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
        public IStash Stash { get; set; }

        public StreamListenerBuilderActor()
        {
            Active();
        }

        //In the active state StreamListeners can be created on demand
        public void Active()
        {
            // gets a message from StreamListenreManager
            //Receive<CreateStreamListenerMessage>( // do the magic)
            //After the message has been sent to stream listener 
            //This actor will schedule message to itself with ValidateStreamListenerCreationMessage 
            //If after 10mins StreamListener hasn't been created 
            //Notify StreamLIstenerManager it failed

            
        }

        //In the busy state the maximum concurrency has been already used and creation 
        //needs to be postponed until later
        public void Busy()
        {
            //Stash messages until CreationCompleted/Failed message is received
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

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// StreamListener Builder is responsible for mamanging concurrent creation of stream listeners
    /// </summary>
    public class StreamListenerBuilderActor : ReceiveActor 
    {
        public StreamListenerBuilderActor()
        {
            
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
        internal IResource Resource { get; set; }
    }

    internal class CreationCompleted
    {

    }

    internal class CreationFailed
    {
        private string FixtureId { get; set; }
    }

    #endregion

}

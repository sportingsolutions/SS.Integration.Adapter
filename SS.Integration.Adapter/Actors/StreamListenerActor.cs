using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This class is responsible for managing resource and streaming 
    /// </summary>
    public class StreamListenerActor
    {
        //Before the first snapshot is processed it starts in the NotStarted state
        public void NotStarted()
        {
            //All messages are stashed until it changes state
            //Once completed it sends message
            //StreamListenerCreationCompletedMessage() -> StreamListenerBuilderActor

            //Create HealthCheckActor()
        }

        //Initialised but not streaming yet - this can happen when you start fixture in Setup
        public void Ready()
        {

        }

        //Connected and streaming state - all messages should be processed
        public void Streaming()
        {
            // Sends feed messages to plugin for processing 
            // Sends messages to healthcheck Actor to validate time and sequences
        }

        //Suspends the fixture and sends message to Stream Listener Manager
        public void Disconnected()
        {
            //All futher messages are discarded
            //StreamDisconnectedMessage
           
        }

        //Match over has been processed no further messages should be accepted 
        public void Finished()
        {
            //Match over arrived it should disconnect and let StreamListenerManager now it's completed
        }

        // Happy to break it down further to FeedProcessorActor in order to include all business logic around Sequences and Snapshots

    }
    /*
    #region Private messages

    public class TakeSnapshotMsg
    {
    }

    #endregion

    #region Public messages

    public StreamDisconnectedMsg()
    {
        //if you find you often need to create a simlar messages please create a base class with this property and inherit
        internal string FixtureId { get; set; }
    }

public StreamHealthCheckMessage()
    {
        string FixtureId
        int sequence 
        DateTime Received
    }

    #endregion
    */
}

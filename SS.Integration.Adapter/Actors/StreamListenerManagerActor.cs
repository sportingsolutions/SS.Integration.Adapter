using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Adapter.Actors
{
    //This actor manages all StreamListeners 
    public class StreamListenerManagerActor
    {
        // Receive<CreateStreamListenerMessage> -> Validate whether Stream Listener exists and if not passes it on to StreamListenerBuilderActor
                                                  // Use Resource current details to further validate StreamListener (same as now)

        // Receive<StreamDisconnected> -> Removes StreamListener and recreates a new one using a NEW resource
        // Receive<FixtureCompletedMsg> -> Removes StreamListener and cleans up
        // Receive<StreamListenerCreationFailed> -> Remove the current one and recreate with a new Resource
        
        
    }

    
}

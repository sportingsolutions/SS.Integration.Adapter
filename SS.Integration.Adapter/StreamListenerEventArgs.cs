using System;
using System.Runtime.Serialization;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter
{
    public class StreamListenerEventArgs
    {
        public IListener Listener { get; set; }
        public Exception Exception { get; set; }
        public int Epoch { get; set; }
        public int CurrentSequence { get; set; }
    }
}
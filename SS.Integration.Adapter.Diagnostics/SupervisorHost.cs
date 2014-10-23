using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;

namespace SS.Integration.Adapter.Diagnostics.Host
{
    public class SupervisorHost
    {
        private static IDisposable _hostApi;
        private static Task _workerThread = null;

        public static void Start()
        {
            if(_hostApi != null)
                return;

            string url = "http://localhost:1234";
            _workerThread = Task.Factory.StartNew(() => { _hostApi = WebApp.Start(url); }, TaskCreationOptions.LongRunning);
        }
        
        public static void Stop()
        {
            lock (_workerThread) 
            {
                if (_hostApi == null || _workerThread == null) return;

                _hostApi.Dispose();
                _hostApi = null;

                //in case there's an exception thrown while bringing the server down
                _workerThread.Wait();
            }
        }


    }
}

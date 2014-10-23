﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Owin.Cors;
using Owin;

namespace SS.Integration.Adapter.Diagnostics.Host
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseCors(CorsOptions.AllowAll);
            
            app.MapSignalR();
        }
    }
}

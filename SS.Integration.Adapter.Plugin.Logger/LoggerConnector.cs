//Copyright 2014 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System.Collections.Generic;
using log4net;
using System.ComponentModel.Composition;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;


namespace SS.Integration.Adapter.Plugin.Logger
{
    [Export(typeof(IAdapterPlugin))]
    public class LoggerConnector : IAdapterPlugin
    {
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(LoggerConnector));

        public LoggerConnector()
        {
            _Logger.InfoFormat("LoggerConnector plugin constructor called");
        }

        public void Initialise()
        {
            _Logger.Info("Logger Plugin initialised");
        }

        public void ProcessSnapshot(Fixture fixture, bool hasEpochChanged = false)
        {
            _Logger.InfoFormat("Received snapshot for {0} (hasEpochChanged={1})", fixture, hasEpochChanged);
        }

        public void ProcessStreamUpdate(Fixture fixture, bool hasEpochChanged = false)
        {
            _Logger.InfoFormat("Received delta snapshot for {0} (hasEpochChanged={1}), written to queue at: {2}", fixture, hasEpochChanged,fixture.TimeStamp);
        }

        public void ProcessMatchStatus(Fixture fixture)
        {
            _Logger.InfoFormat("Request for processing Match Statuf of {0} received", fixture);
        }

        public void ProcessFixtureDeletion(Fixture fixture)
        {
            _Logger.InfoFormat("Request for delete {0} received", fixture);
        }

        public void UnSuspend(Fixture fixture)
        {
            _Logger.InfoFormat("Request for un-suspend {0} received", fixture);
        }

        public void Suspend(string fixtureId)
        {
            _Logger.InfoFormat("Request for suspend FixtureId={0} received", fixtureId);
        }

        public void Dispose()
        {
            _Logger.Info("Request for disposing Logger plugin received");
        }

        public IEnumerable<IMarketRule> MarketRules
        {
            get { return new List<IMarketRule>(); }
        }
    }
}

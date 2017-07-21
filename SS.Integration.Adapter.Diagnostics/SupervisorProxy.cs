using System;
using System.Collections.Generic;
using Akka.Actor;
using SS.Integration.Adapter.Diagnostics.Model.Service.Interface;
using SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;

namespace SS.Integration.Adapter.Diagnostics
{
    public class SupervisorProxy : ISupervisorProxy
    {
        #region Private members

        private IActorRef _supervisorActor;

        #endregion

        #region Constructors

        public SupervisorProxy(IActorRef supervisorActor)
        {
            _supervisorActor = supervisorActor ?? throw new ArgumentNullException(nameof(supervisorActor));
        }

        #endregion

        #region Imlementation of ISupervisorProxy

        public IEnumerable<ISportOverview> GetSports()
        {
            throw new NotImplementedException();
        }

        public ISportDetails GetSportDetail(string sportCode)
        {
            throw new NotImplementedException();
        }

        public IFixtureDetails GetFixtureDetail(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public IAdapterStatus GetAdapterStatus()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IFixtureProcessingEntry> GetFixtureHistory(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IFixtureOverview> GetFixtures()
        {
            throw new NotImplementedException();
        }

        public bool TakeSnapshot(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public bool RestartListener(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public bool ClearState(string fixtureId)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}

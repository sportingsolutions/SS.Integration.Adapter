using System;
using System.Reflection;
using Akka.Actor;
using Akka.TestKit.NUnit;

namespace SS.Integration.Adapter.Tests
{
    public class BaseTestKit : TestKit
    {
        protected IActorRef GetChildActorRef(IActorRef anchorRef,string name)
        {
            return Sys.ActorSelection(anchorRef, name).ResolveOne(TimeSpan.FromSeconds(5)).Result;
        }

        protected TActor GetUnderlyingActor<TActor>(IActorRef actorRef) where TActor : ActorBase
        {
            var actorProp =
                typeof(ActorCell).GetProperty(
                    "Actor",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty);

            return actorProp?.GetValue((ActorCell)((LocalActorRef)actorRef)?.Underlying) as TActor;
        }
    }
}

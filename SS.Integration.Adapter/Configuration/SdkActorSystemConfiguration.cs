//Copyright 2017 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

namespace SS.Integration.Adapter.Configuration
{
    /// <summary>
    /// Akka HOCON for the SDK's own actor system (SDKSystem).
    /// 
    /// The SDK creates SDKSystem with ActorSystem.Create("SDKSystem") and no explicit configuration, so on .NET Framework
    /// it reads the akka section of the host's application configuration directly. That is where the SDK's
    /// echocontrolleractor-mailbox is already defined, and it is where these blocks live in the adapter's App.config.
    /// This class holds the same text as the single source for the tests and the README; it is not applied in code
    /// (the adapter cannot pass configuration to the SDK's system). A host that supplies HOCON to SDKSystem by another
    /// route must add the same blocks there.
    /// 
    /// echo-controller-dispatcher: a PinnedDispatcher for the SDK's EchoControllerActor, which runs the stream liveness
    /// check (echo every 10s, 3s timeout; three misses raise "Stream got disconnected"). On the default dispatcher, i.e.
    /// the shared .NET thread pool, a starved pool delays the tick and the echo POST until the check declares live
    /// streams dead while the AMQP connection is still open. Its own thread removes that dependency for the tick and
    /// the POST. Echo receipt still arrives through RabbitMQ.Client consumer callbacks on the pool.
    /// 
    /// stream-controller-dispatcher: a PinnedDispatcher for the SDK's StreamControllerActor, the single owner of the AMQP
    /// connection, which blocks on BasicCancel (20s RPC timeout) when a stream is unsubscribed. One dedicated thread
    /// also serialises those cancels, which the shared channel requires (concurrent RPCs on one channel fail with
    /// "Pipelining of requests forbidden").
    /// 
    /// Both actors are created by the SDK by name (/user/EchoControllerActor, /user/StreamControllerActor) with a mailbox
    /// but no dispatcher in their Props, so akka.actor.deployment applies to them.
    /// </summary>
    public static class SdkActorSystemConfiguration
    {
        public const string EchoControllerActorName = "EchoControllerActor";
        public const string StreamControllerActorName = "StreamControllerActor";
        public const string EchoControllerDispatcherId = "echo-controller-dispatcher";
        public const string StreamControllerDispatcherId = "stream-controller-dispatcher";

        /// <summary>
        /// The HOCON blocks carried by the adapter's App.config for the SDK's actor system.
        /// </summary>
        public const string DispatchersHocon = @"
            " + EchoControllerDispatcherId + @" {
                type = PinnedDispatcher
                throughput = 1
            }
            " + StreamControllerDispatcherId + @" {
                type = PinnedDispatcher
                throughput = 1
            }
            akka.actor.deployment {
                /" + EchoControllerActorName + @" {
                    dispatcher = " + EchoControllerDispatcherId + @"
                }
                /" + StreamControllerActorName + @" {
                    dispatcher = " + StreamControllerDispatcherId + @"
                }
            }";
    }
}

////Copyright 2014 Spin Services Limited

////Licensed under the Apache License, Version 2.0 (the "License");
////you may not use this file except in compliance with the License.
////You may obtain a copy of the License at

////    http://www.apache.org/licenses/LICENSE-2.0

////Unless required by applicable law or agreed to in writing, software
////distributed under the License is distributed on an "AS IS" BASIS,
////WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
////See the License for the specific language governing permissions and
////limitations under the License.

//using System;
//using System.Collections.Generic;
//using System.Threading;
//using FluentAssertions;
//using Moq;
//using NUnit.Framework;
//using SportingSolutions.Udapi.Sdk.Interfaces;
//using SS.Integration.Adapter.Interface;
//using SS.Integration.Adapter.Model;
//using SS.Integration.Adapter.Model.Enums;
//using SS.Integration.Adapter.Model.Interfaces;

//namespace SS.Integration.Adapter.Tests
//{
//    [TestFixture]
//    [Category("Regression")]
//    class RegressionTests
//    {

//        /// <summary>
//        /// Suppose this case:
//        /// 
//        /// 1) the stream listener has processed a snapshot regarding a fixture that 
//        ///    is still in a Setup state (correctly, because we want to insert it
//        ///    in the downstream system). Let's say that the snapshot has sequence=2.
//        ///    The stream listener maintains the reference of the first resource it has 
//        ///    seen (in this case the resource object with sequence = 2)
//        /// 2) In this case, the last sequence correctly processed by the streamlistener
//        ///    is 2 and therefore we write this information on the EventState. 
//        ///    Unfortunately seq = 2 is also the resource.Sequence's value
//        /// 3) when the fixture change state, we correctly connect to the streaming
//        ///    server ad
//        /// 4) the logic says that after the connection has been established we need to check
//        ///    the resource.Sequence with the last sequence processed (information
//        ///    stored on the EventState object)
//        /// 
//        /// Following this case, we will always end up checking 2 != 2, therefore
//        /// missing potential sequences.
//        /// 
//        /// </summary>
//        [Test]
//        public void AcquireSnapshotAfterFixtureStatusChangeTest()
//        {
//            var settings = new Mock<ISettings>();
//            var service = new Mock<IServiceFacade>();
            
//            var plugin = new Mock<IAdapterPlugin>();
//            var feature = new Mock<IFeature>();
//            var resource = new Mock<IResourceFacade>();
//            var eventstate = new Mock<IEventState>();

//            Fixture fixture = new Fixture {Id = "ABC", FixtureName = "ABC", Sequence = 2, MatchStatus = "10" };
//            fixture.Tags.Add("Sport", "Football");

//            settings.Setup(x => x.FixtureCreationConcurrency).Returns(1);
//            settings.Setup(x => x.FixtureCheckerFrequency).Returns(1000);
//            settings.Setup(x => x.EventStateFilePath).Returns(".");
//            settings.Setup(x => x.ProcessingLockTimeOutInSecs).Returns(10);
//            settings.Setup(x => x.StreamSafetyThreshold).Returns(int.MaxValue);

//            feature.Setup(x => x.Name).Returns("Football");

//            service.Setup(x => x.GetSports()).Returns(new List<IFeature> { feature.Object });
//            service.Setup(x => x.GetResources(It.Is<string>(y => y == "Football"))).Returns(new List<IResourceFacade> { resource.Object });
//            service.Setup(x => x.IsConnected).Returns(true);

//            eventstate.Setup(x => x.GetFixtureState(It.Is<string>(y => y == "ABC"))).Returns(
//                new FixtureState 
//                { 
//                    MatchStatus = MatchStatus.Setup, 
//                    Sequence = 2,
//                    Id = "ABC",
//                    Sport = "Football"
//                });

//            resource.Setup(x => x.Id).Returns("ABC");
//            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Setup);
//            resource.Setup(x => x.Content).Returns(new Summary
//            {
//                Id = "ABC",
//                MatchStatus = 10, // InSetup
//                Sequence = 2
//            });

//            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));
//            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);

//            Adapter adapter = new Adapter(settings.Object, service.Object, plugin.Object);

//            adapter.Start();


//            Thread.Sleep(3000);

//            resource.Verify( x => x.GetSnapshot(), Times.Once);
//            resource.Verify( x => x.StartStreaming(), Times.Never);

//            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.Prematch);
//            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
//            resource.Setup(x => x.Content).Returns(new Summary
//                {
//                    Id= "ABC",
//                    MatchStatus = 30,
//                    Sequence =3 
//                }); 

//            service.Setup(x => x.GetResources(It.Is<string>(y => y == "Football"))).Returns(new List<IResourceFacade> { resource.Object });

//            Thread.Sleep(3000);

//            resource.Verify(x => x.GetSnapshot(), Times.Exactly(2));
//        }

//        /// <summary>
//        /// See CINT-280
//        /// </summary>
//        [Test]
//        public void AllowReconnectionAfterDisconnection()
//        {
//            Mock<IResourceFacade> resource = new Mock<IResourceFacade>();
//            Mock<IAdapterPlugin> connector = new Mock<IAdapterPlugin>();
//            Mock<IEventState> state = new Mock<IEventState>();
//            Mock<ISettings> settings = new Mock<ISettings>();

//            var provider = new StateManager(settings.Object, connector.Object);

//            Fixture fixture = new Fixture { Id = "Reconnect", Sequence = 1, MatchStatus = ((int)MatchStatus.InRunning).ToString() };

//            resource.Setup(x => x.Content).Returns(new Summary());
//            resource.Setup(x => x.MatchStatus).Returns(MatchStatus.InRunning);
//            resource.Setup(r => r.Id).Returns("Reconnect");
//            resource.Setup(x => x.StartStreaming()).Raises(x => x.StreamConnected += null, EventArgs.Empty);
//            resource.Setup(x => x.GetSnapshot()).Returns(FixtureJsonHelper.ToJson(fixture));

//            StreamListener listener = new StreamListener(resource.Object, connector.Object, state.Object, provider,settings.Object);

//            listener.Start();

//            listener.IsStreaming.Should().BeTrue();

//            listener.ResourceOnStreamDisconnected(this, EventArgs.Empty);

//            listener.IsStreaming.Should().BeFalse();

//            listener.UpdateResourceState(resource.Object);

//            listener.IsStreaming.Should().BeTrue();
//        }
//    }
//}

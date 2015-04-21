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
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Configuration;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.ProcessState;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class EventStateTests
    {
        [Test]
        public void ShouldLoadPreviuosState()
        {
            var storeProvider = new Mock<IStoreProvider>();
            storeProvider.Setup(sp => sp.Read(It.IsAny<string>())).Returns("{\"12345\": { \"Id\" : \"12345\", \"Sequence\": 6}}");

            var eventState = EventState.Create(storeProvider.Object, new Settings());

            var currentSeq = eventState.GetCurrentSequence("Tennis", "12345");
            currentSeq.Should().Be(6);
        }

        [Test]
        public void ShouldAddAndRemoveFixtures()
        {
            var storeProvider = new Mock<IStoreProvider>();
            storeProvider.Setup(sp => sp.Read(It.IsAny<string>())).Throws(new FileNotFoundException());

            var eventState = EventState.Create(storeProvider.Object, new Settings());

            eventState.AddFixture("football", "1", 1, 1);

            var currentSeq = eventState.GetCurrentSequence("football", "1");
            currentSeq.Should().Be(1);

            eventState.RemoveFixture("football", "1");
            currentSeq = eventState.GetCurrentSequence("football", "1");
            currentSeq.Should().Be(-1);
        }

        [Test]
        public void ShouldSequenceBeUpdated()
        {
            var storeProvider = new Mock<IStoreProvider>();
            storeProvider.Setup(sp => sp.Read(It.IsAny<string>())).Throws(new FileNotFoundException());

            var eventState = EventState.Create(storeProvider.Object, new Settings());

            eventState.AddFixture("football", "1", 1, 1);
            eventState.AddFixture("football", "1", 2, 1);
            eventState.AddFixture("football", "1", 3, 1);

            var currentSeq = eventState.GetCurrentSequence("football", "1");
            currentSeq.Should().Be(3);

            eventState.AddFixture("football", "1", 4, 1);

            currentSeq = eventState.GetCurrentSequence("football", "1");
            currentSeq.Should().Be(4);
        }

        [Test]
        public void ShouldRemoveInactiveFixtures()
        {
            var storeProvider = new Mock<IStoreProvider>();
            storeProvider.Setup(sp => sp.Read(It.IsAny<string>())).Throws(new FileNotFoundException());

            var eventState = EventState.Create(storeProvider.Object, new Settings());

            eventState.AddFixture("basketball", "1", 1, 1);
            eventState.AddFixture("football", "2", 2, 1);
            eventState.AddFixture("football", "4", 3, 1);

            var activeOne = new Mock<IResourceFacade>();
            activeOne.Setup(f => f.Id).Returns("1");
            activeOne.Setup(f => f.IsMatchOver).Returns(false);
            var activeThree = new Mock<IResourceFacade>();
            activeThree.Setup(f => f.Id).Returns("3");
            activeThree.Setup(f => f.IsMatchOver).Returns(false);
            var inactiveFour = new Mock<IResourceFacade>();
            inactiveFour.Setup(f => f.Id).Returns("4");
            inactiveFour.Setup(f => f.IsMatchOver).Returns(true);

            var activeList = new List<IResourceFacade> { activeOne.Object, activeThree.Object, inactiveFour.Object };

            eventState.RemoveInactiveFixtures("football", activeList);

            var currentSeq = eventState.GetCurrentSequence("basketball", "1");
            currentSeq.Should().Be(1);
            currentSeq = eventState.GetCurrentSequence("football", "2");
            currentSeq.Should().Be(-1);
            currentSeq = eventState.GetCurrentSequence("football", "3");
            currentSeq.Should().Be(-1);
            currentSeq = eventState.GetCurrentSequence("football", "4");
            currentSeq.Should().Be(-1);
        }

        [Test]
        public void ShouldWorkWithMultiSports()
        {
            var storeProvider = new Mock<IStoreProvider>();
            storeProvider.Setup(sp => sp.Read(It.IsAny<string>())).Throws(new FileNotFoundException());

            var eventState = EventState.Create(storeProvider.Object, new Settings());

            eventState.AddFixture("football", "1", 1, 1);
            eventState.AddFixture("tennis", "2", 2, 1);
            eventState.AddFixture("rugby", "3", 3, 1);

            var currentSeq = eventState.GetCurrentSequence("football", "1");
            currentSeq.Should().Be(1);
            currentSeq = eventState.GetCurrentSequence("tennis", "2");
            currentSeq.Should().Be(2);
            currentSeq = eventState.GetCurrentSequence("rugby", "3");
            currentSeq.Should().Be(3);
        }
    }
}

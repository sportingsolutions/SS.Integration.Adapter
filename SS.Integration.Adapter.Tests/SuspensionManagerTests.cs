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

using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Exceptions;
using SS.Integration.Adapter.MarketRules.Interfaces;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class SuspensionManagerTests
    {
        #region Constants

        public const string SUSPENSION_MANAGER_CATEGORY = nameof(SuspensionManager);

        #endregion

        #region Properties

        protected Mock<IStateProvider> StateProviderMock;
        protected Mock<IAdapterPlugin> AdapterPluginMock;

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            StateProviderMock = new Mock<IStateProvider>();
            AdapterPluginMock = new Mock<IAdapterPlugin>();
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures we skip fixture suspension when markets state null
        /// </summary>
        [Test]
        [Category(SUSPENSION_MANAGER_CATEGORY)]
        public void SkipSuspensionWhenMarketsStateNull()
        {
            //Arrange
            var fixture1 = new Fixture { Id = "fixture1Id" };
            StateProviderMock.Setup(a =>
                    a.GetMarketsState(It.IsAny<string>()))
                .Returns((IMarketStateCollection)null);
            var suspensionManager =
                new SuspensionManager(
                    StateProviderMock.Object,
                    AdapterPluginMock.Object);

            //Act
            suspensionManager.Suspend(fixture1);

            //Assert
            StateProviderMock.Verify(o =>
                    o.GetMarketsState(It.Is<string>(f => f.Equals(fixture1.Id))),
                Times.Once);
            AdapterPluginMock.Verify(o =>
                    o.Suspend(It.Is<string>(f => f.Equals(fixture1.Id))),
                Times.Never);
        }

        /// <summary>
        /// This test ensures we correctly execute fixture suspension
        /// </summary>
        [Test]
        [Category(SUSPENSION_MANAGER_CATEGORY)]
        public void SuspendFixtureTest()
        {
            //Arrange
            var fixture1 = new Fixture { Id = "fixture1Id" };
            var marketStateCollectionMock = new Mock<IMarketStateCollection>();
            marketStateCollectionMock.SetupGet(o => o.FixtureId).Returns(fixture1.Id);
            StateProviderMock.Setup(a =>
                    a.GetMarketsState(It.IsAny<string>()))
                .Returns(marketStateCollectionMock.Object);
            var suspensionManager =
                new SuspensionManager(
                    StateProviderMock.Object,
                    AdapterPluginMock.Object);

            //Act
            suspensionManager.Suspend(fixture1, SuspensionReason.FIXTURE_DISPOSING);
            suspensionManager.Suspend(fixture1, SuspensionReason.SUSPENSION);
            suspensionManager.Suspend(fixture1, SuspensionReason.DISCONNECT_EVENT);
            suspensionManager.Suspend(fixture1, SuspensionReason.FIXTURE_ERRORED);
            suspensionManager.Suspend(fixture1, SuspensionReason.FIXTURE_DELETED);

            //Assert
            StateProviderMock.Verify(o =>
                    o.GetMarketsState(It.Is<string>(f => f.Equals(fixture1.Id))),
                Times.Exactly(5));
            AdapterPluginMock.Verify(o =>
                    o.Suspend(It.Is<string>(f => f.Equals(fixture1.Id))),
                Times.Exactly(4));
            AdapterPluginMock.Verify(o =>
                    o.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(fixture1.Id)), false),
                Times.Once);
            AdapterPluginMock.Verify(o =>
                    o.ProcessStreamUpdate(It.IsAny<Fixture>(), true),
                Times.Never);
        }

        /// <summary>
        /// This test ensures we throw plugin exception when plugin throws error
        /// </summary>
        [Test]
        [Category(SUSPENSION_MANAGER_CATEGORY)]
        public void SuspendFixtureThrowsPluginExceptionTest()
        {
            //Arrange
            var fixture1 = new Fixture { Id = "fixture1Id" };
            var marketStateCollectionMock = new Mock<IMarketStateCollection>();
            marketStateCollectionMock.SetupGet(o => o.FixtureId).Returns(fixture1.Id);
            StateProviderMock.Setup(a =>
                    a.GetMarketsState(It.IsAny<string>()))
                .Returns(marketStateCollectionMock.Object);
            AdapterPluginMock.Setup(o => o.Suspend(It.Is<string>(f => f.Equals(fixture1.Id)))).Throws<Exception>();
            var suspensionManager =
                new SuspensionManager(
                    StateProviderMock.Object,
                    AdapterPluginMock.Object);

            //Act
            void SuspendCall() => suspensionManager.Suspend(fixture1);

            //Assert
            Assert.Throws<PluginException>(SuspendCall);
            StateProviderMock.Verify(o =>
                    o.GetMarketsState(It.Is<string>(f => f.Equals(fixture1.Id))),
                Times.Once);
            AdapterPluginMock.Verify(o =>
                    o.Suspend(It.Is<string>(f => f.Equals(fixture1.Id))),
                Times.Once);
        }

        /// <summary>
        /// This test ensures we correctly execute fixture unsuspention
        /// </summary>
        [Test]
        [Category(SUSPENSION_MANAGER_CATEGORY)]
        public void UnsuspendFixtureTest()
        {
            //Arrange
            var fixture1 = new Fixture { Id = "fixture1Id" };
            var marketStateMock = new Mock<IMarketState>();
            marketStateMock.SetupGet(o => o.Id).Returns("market1Id");
            marketStateMock.SetupGet(o => o.IsForcedSuspended).Returns(true);
            var marketStateCollectionMock = new Mock<IUpdatableMarketStateCollection>();
            marketStateCollectionMock.SetupGet(o => o.FixtureId).Returns(fixture1.Id);
            marketStateCollectionMock.SetupGet(o => o.Markets).Returns(new[] { marketStateMock.Object.Id });
            marketStateCollectionMock.SetupGet(o => o[It.IsAny<string>()]).Returns(marketStateMock.Object);
            StateProviderMock.Setup(a =>
                    a.GetMarketsState(It.IsAny<string>()))
                .Returns(marketStateCollectionMock.Object);
            var suspensionManager =
                new SuspensionManager(
                    StateProviderMock.Object,
                    AdapterPluginMock.Object);

            //Act
            suspensionManager.Unsuspend(fixture1);

            //Assert
            StateProviderMock.Verify(o =>
                    o.GetMarketsState(It.Is<string>(f => f.Equals(fixture1.Id))),
                Times.Once);
            AdapterPluginMock.Verify(o =>
                    o.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(fixture1.Id))),
                Times.Once);
            AdapterPluginMock.Verify(o =>
                    o.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(fixture1.Id)), false),
                Times.Once);
            AdapterPluginMock.Verify(o =>
                    o.ProcessStreamUpdate(It.IsAny<Fixture>(), true),
                Times.Never);
            marketStateCollectionMock.Verify(o =>
                    o.OnMarketsForcedUnsuspension(It.IsAny<IEnumerable<IMarketState>>()),
                Times.Once);
        }

        /// <summary>
        /// This test ensures we throw plugin exception when plugin throws error
        /// </summary>
        [Test]
        [Category(SUSPENSION_MANAGER_CATEGORY)]
        public void UnsuspendFixtureThrowsPluginExceptionTest()
        {
            //Arrange
            var fixture1 = new Fixture { Id = "fixture1Id" };
            var marketStateMock = new Mock<IMarketState>();
            marketStateMock.SetupGet(o => o.Id).Returns("market1Id");
            marketStateMock.SetupGet(o => o.IsForcedSuspended).Returns(true);
            var marketStateCollectionMock = new Mock<IUpdatableMarketStateCollection>();
            marketStateCollectionMock.SetupGet(o => o.FixtureId).Returns(fixture1.Id);
            marketStateCollectionMock.SetupGet(o => o.Markets).Returns(new[] { marketStateMock.Object.Id });
            marketStateCollectionMock.SetupGet(o => o[It.IsAny<string>()]).Returns(marketStateMock.Object);
            StateProviderMock.Setup(a =>
                    a.GetMarketsState(It.IsAny<string>()))
                .Returns(marketStateCollectionMock.Object);
            AdapterPluginMock.Setup(o => o.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(fixture1.Id)))).Throws<Exception>();
            var suspensionManager =
                new SuspensionManager(
                    StateProviderMock.Object,
                    AdapterPluginMock.Object);

            //Act
            void UnsuspendCall() => suspensionManager.Unsuspend(fixture1);

            //Assert
            Assert.Throws<PluginException>(UnsuspendCall);
            StateProviderMock.Verify(o =>
                    o.GetMarketsState(It.Is<string>(f => f.Equals(fixture1.Id))),
                Times.Once);
            AdapterPluginMock.Verify(o =>
                    o.UnSuspend(It.Is<Fixture>(f => f.Id.Equals(fixture1.Id))),
                Times.Once);
            AdapterPluginMock.Verify(o =>
                    o.ProcessStreamUpdate(It.Is<Fixture>(f => f.Id.Equals(fixture1.Id)), false),
                Times.Once);
            AdapterPluginMock.Verify(o =>
                    o.ProcessStreamUpdate(It.IsAny<Fixture>(), true),
                Times.Never);
            marketStateCollectionMock.Verify(o =>
                    o.OnMarketsForcedUnsuspension(It.IsAny<IEnumerable<IMarketState>>()),
                Times.Once);
        }

        #endregion
    }
}


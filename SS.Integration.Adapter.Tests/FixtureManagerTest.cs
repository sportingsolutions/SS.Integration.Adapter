//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;
//using Moq;
//using NUnit.Framework;
//using SS.Integration.Adapter.Interface;

//namespace SS.Integration.Adapter.Tests
//{
//    [TestFixture]
//    public class FixtureManagerTest
//    {

//        [Test]
//        public void DispouseTest()
//        {
//            var service = new Mock<IServiceFacade>();
//            var fixtureOne = new Mock<IResourceFacade>();
//            var fixtureTwo = new Mock<IResourceFacade>();
//            service.Setup(s => s.GetResources("Football")).Returns(new List<IResourceFacade> { fixtureOne.Object, fixtureTwo.Object });
//            var streamManager = new Mock<IStreamListenerManager>();

//            var testing = new FixtureManager(5, streamManager.Object, service.Object.GetResources);
//            testing.Dispose();
//            Thread.Sleep(10000);


//        }
//    }
//}

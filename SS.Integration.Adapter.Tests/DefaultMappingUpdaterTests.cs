using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Mappings;
using SS.Integration.Adapter.Model.Interfaces;
using SS.Integration.Adapter.Plugin.Model;
using SS.Integration.Common.ConfigSerializer;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    class DefaultMappingUpdaterTests
    {
        private DefaultMappingUpdater _mapUpd;
        private Mock<IObjectProvider<List<Mapping>>> _mockObjProv;
        private Mock<ISportConfigSerializer> _mockSportConfSer;
        private string[] _sports = new string[] { "a", "b", "c" };

        [SetUp]
        public void SetUp()
        {
            _mockObjProv = new Mock<IObjectProvider<List<Mapping>>>();
            _mockSportConfSer = new Mock<ISportConfigSerializer>();
            _mapUpd  = new DefaultMappingUpdater();
            _mapUpd.CachedObjectProvider = _mockObjProv.Object;
            _mapUpd.Serializer = _mockSportConfSer.Object;
            _mockSportConfSer.Setup(cs => cs.GetSportsList(It.IsAny<string>())).Returns(_sports);
        }

        [Test]
        public void ShouldLoadJustCached()
        {
            _mockObjProv.Setup(op => op.GetObject(It.IsAny<string>())).Returns(new List<Mapping>());
            _mapUpd.Initialize();
            _mockSportConfSer.Verify(cs => cs.GetSportsList(It.IsAny<string>()),Times.Once());
            _mockSportConfSer.Verify(cs => cs.Deserialize<CompetitionMapping>(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            _mockSportConfSer.Verify(cs => cs.Deserialize<MarketMapping>(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void ShouldLoadFromSerializer()
        {
            _mockObjProv.Setup(op => op.GetObject(It.IsAny<string>())).Returns((List<Mapping>)null);
            _mapUpd.Initialize();
            _mockSportConfSer.Verify(cs => cs.GetSportsList(It.IsAny<string>()), Times.Once());
            _mockSportConfSer.Verify(cs => cs.Deserialize<CompetitionMapping>(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(_sports.Length));
            _mockSportConfSer.Verify(cs => cs.Deserialize<MarketMapping>(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(_sports.Length));
        }



    }
}

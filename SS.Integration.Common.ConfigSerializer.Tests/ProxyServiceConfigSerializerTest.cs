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
using System.Linq;
using Google.GData.Client;
using Google.GData.Spreadsheets;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Plugin.Model;
using SS.Integration.Mapping.ProxyService.Client;
using SS.Integration.Mapping.ProxyService.Model;

namespace SS.Integration.Common.ConfigSerializer.Tests
{
    [TestFixture]
    class ProxyServiceConfigSerializerTest
    {

        private string _company = "TestCompany";
        private string _enviroment = "UnitTests";
        private string _sport = "Basketball";

        private ProxyServiceConfigSerializer _serializer =
            new ProxyServiceConfigSerializer();

       
        [Test]
        public void ReadMappingRequestTest()
        {
             Mock<IProxyServiceClient> clientMock = new Mock<IProxyServiceClient>();
            _serializer.Client = clientMock.Object;
            _serializer.Settings = new ProxyServiceSettings()
                {
                    Company = _company,
                    Enviroment = _enviroment,
                    CheckUpdateServiceUrl = "---",
                    ReadMappingServiceUrl = "---",
                    SportListServiceUrl = "---"
                };
            _serializer.Deserialize<object>(MappingCategory.CompetitionMapping.ToString(), _sport);

            clientMock.Verify( it => it.ReadMappings<object>(
        
                        It.Is<MappingReadRequest>( req => 
        
                            req.Company == _company && 
                            req.Enviroment == _enviroment &&
                            req.MappingType == MappingType.CompetitionMapping &&
                            req.Sport == _sport)
                ));

            _serializer.Deserialize<object>(MappingCategory.MarketMapping.ToString(), _sport);

            clientMock.Verify( it => it.ReadMappings<object>(
        
                        It.Is<MappingReadRequest>( req => 
        
                            req.Company == _company && 
                            req.Enviroment == _enviroment &&
                            req.MappingType == MappingType.MarketMapping &&
                            req.Sport == _sport)
                ));
        }

         [Test]
        public void CheckUpdateRequestTest()
        {
            Mock<IProxyServiceClient> clientMock = new Mock<IProxyServiceClient>();
            _serializer.Client = clientMock.Object;
            _serializer.Settings = new ProxyServiceSettings()
                {
                    Company = _company,
                    Enviroment = _enviroment,
                    CheckUpdateServiceUrl = "---",
                    ReadMappingServiceUrl = "---",
                    SportListServiceUrl = "---"
                };
            _serializer.IsUpdateNeeded(MappingCategory.CompetitionMapping.ToString(), _sport);

            clientMock.Verify( it => it.CheckUpdate(
        
                        It.Is<MappingCheckUpdateRequest>( req => 
        
                            req.Company == _company && 
                            req.Enviroment == _enviroment &&
                            req.MappingType == MappingType.CompetitionMapping &&
                            req.Sport == _sport)
                ));

            _serializer.IsUpdateNeeded(MappingCategory.MarketMapping.ToString(), _sport);

            clientMock.Verify(it => it.CheckUpdate(

                        It.Is<MappingCheckUpdateRequest>(req => 
        
                            req.Company == _company && 
                            req.Enviroment == _enviroment &&
                            req.MappingType == MappingType.MarketMapping &&
                            req.Sport == _sport)
                ));
        }


         [Test]
         public void SportsListTest()
         {
             Mock<IProxyServiceClient> clientMock = new Mock<IProxyServiceClient>();
             _serializer.Client = clientMock.Object;
             _serializer.Settings = new ProxyServiceSettings()
             {
                 Company = _company,
                 Enviroment = _enviroment,
                 CheckUpdateServiceUrl = "---",
                 ReadMappingServiceUrl = "---",
                 SportListServiceUrl = "---"
             };

             _serializer.GetSportsList("---");

             clientMock.Verify(it => it.SportsList(

                         It.Is<MappingRequest>(req =>
                             req.Company == _company &&
                             req.Enviroment == _enviroment
                 )));


         }
     


    }
}

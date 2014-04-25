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
using System.Runtime.CompilerServices;
using SS.Integration.Mapping.ProxyService.Client;
using SS.Integration.Mapping.ProxyService.Model;

namespace SS.Integration.Common.ConfigSerializer
{

    public class ProxyServiceConfigSerializer : ISportConfigSerializer
    {

        #region Properties

        public ProxyServiceSettings Settings { get; set; }

        public IProxyServiceClient Client { get; set; }

        #endregion

        #region Constructors


        public ProxyServiceConfigSerializer()
        {

        }

        public ProxyServiceConfigSerializer(ProxyServiceSettings settings)
        {
            this.Settings = settings;
            this.Client = new ProxyServiceClient(settings.ReadMappingServiceUrl, 
                                                 settings.CheckUpdateServiceUrl,
                                                 settings.SportListServiceUrl);

            
        }

        #endregion

        #region PrivateMethods

        public void SettingsAndClientCheck()
        {
            if (this.Client == null)
                throw new ArgumentNullException("Client", "Client cannot be null");
            if (this.Settings == null)
                throw new ArgumentNullException("Settings", "Settings cannot be null");
        }

        private MappingType GetFromMappingCategory(string mappingCategoryDescription)
        {
            MappingCategory cat;
            bool found = Enum.TryParse(mappingCategoryDescription, true, out cat);

            if (!found)
            {
                throw new ArgumentException(String.Format("category \"{0}\"not recognized",mappingCategoryDescription));
            }

            switch (cat)
            {
                case MappingCategory.CompetitionMapping:
                    return MappingType.CompetitionMapping;
                case MappingCategory.MarketMapping:
                    return MappingType.MarketMapping;
                default:
                    throw new ArgumentException("category not recognized");
            }
        }

        #endregion

        #region Implementation


        public List<T> Deserialize<T>(string fileNameOrReference) where T : class, new()
        {
            SettingsAndClientCheck();
            var request = new MappingReadRequest()
                {
                    Company = this.Settings.Company,
                    Enviroment = this.Settings.Enviroment,
                    MappingType = GetFromMappingCategory(fileNameOrReference)
                };
            
            return this.Client.ReadMappings<T>(request);
        }

        public List<T> Deserialize<T>(string fileNameOrReference, string sportName)
            where T : class,new()
        {
            SettingsAndClientCheck();
            var request = new MappingReadRequest()
            {
                Company = this.Settings.Company,
                Enviroment = this.Settings.Enviroment,
                MappingType = GetFromMappingCategory(fileNameOrReference),
                Sport = sportName
            };
            return this.Client.ReadMappings<T>(request);
        }

        public void Serialize<T>(List<T> settings, string fileNameOrReference)
        {
            throw new NotImplementedException();
        }

        public void Serialize<T>(List<T> settings, string fileNameOrReference, string sportName)
        {
            throw new NotImplementedException();
        }

        public bool IsUpdateNeeded(string fileNameOrReference)
        {
            SettingsAndClientCheck();
            var request = new MappingCheckUpdateRequest()
            {
                Company = this.Settings.Company,
                Enviroment = this.Settings.Enviroment,
                MappingType = GetFromMappingCategory(fileNameOrReference)
            };
            return this.Client.CheckUpdate(request);
        }

        public bool IsUpdateNeeded(string fileNameOrReference,string sportName)
        {
            SettingsAndClientCheck();
            var request = new MappingCheckUpdateRequest()
            {
                Company = this.Settings.Company,
                Enviroment = this.Settings.Enviroment,
                MappingType = GetFromMappingCategory(fileNameOrReference),
                Sport = sportName
            };
            return this.Client.CheckUpdate(request);
        }


        public string[] GetSportsList(string fileNameOrReference)
        {
            SettingsAndClientCheck();
            var request = new MappingRequest()
            {
                Company = this.Settings.Company,
                Enviroment = this.Settings.Enviroment
            };
            return this.Client.SportsList(request);
        }

        #endregion

    }
}

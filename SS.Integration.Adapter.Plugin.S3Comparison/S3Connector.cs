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
using System.ComponentModel.Composition;
using System.Configuration;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;


namespace SS.Integration.Adapter.Plugin.S3Comparison
{
    [Export(typeof(IAdapterPlugin))]
    public class LoggerConnector : IAdapterPlugin
    {
        private static readonly string AwsAccessKey = ConfigurationManager.AppSettings["Comparison.AWSAccessKey"];
        private static readonly string AwsSecretKey = ConfigurationManager.AppSettings["Comparison.AWSSecretKey"];
        private static readonly string AwsBucket = ConfigurationManager.AppSettings["Comparison.AWSBucket"];
        private static readonly string ServiceEnv = ConfigurationManager.AppSettings["Comparison.ServiceEnvironment"];
        private static readonly string ServiceMode = ConfigurationManager.AppSettings["Comparison.ServiceMode"];

        public void Initialise()
        {

        }

        public void ProcessSnapshot(Fixture fixture, bool hasEpochChanged = false)
        {
            WriteToS3(fixture, true);
        }

        public void ProcessStreamUpdate(Fixture fixture, bool hasEpochChanged = false)
        {
            WriteToS3(fixture);
        }

        public void ProcessMatchStatus(Fixture fixture)
        {

        }

        public void ProcessFixtureDeletion(Fixture fixture)
        {

        }

        public void UnSuspend(Fixture fixture)
        {

        }

        public void Suspend(string fixtureId)
        {

        }

        public void Dispose()
        {

        }

        public IEnumerable<IMarketRule> MarketRules => new List<IMarketRule>();

        private static void WriteToS3(Fixture fixture, bool isSnapshot = false)
        {
            using (var client = new AmazonS3Client(
                new BasicAWSCredentials(AwsAccessKey, AwsSecretKey),
                Amazon.RegionEndpoint.EUWest1))
            {
                var now = DateTime.UtcNow;
                var date = now.ToString("yyyyMMdd");
                var time = now.ToString("HHmmss");
                var objectType = isSnapshot ? "snapshot" : "delta";
                
                var fixtureNormalized = fixture.FixtureName.Replace(" ", string.Empty);

                var key = $"adapter-comparisons/{ServiceEnv}/{date}/{fixtureNormalized}/{ServiceMode}/{fixture.Sequence}-{objectType}-{time}.json";

                var request = new PutObjectRequest
                {
                    BucketName = AwsBucket,
                    Key = key,
                    ContentBody = JsonConvert.SerializeObject(fixture, Formatting.Indented),
                    ContentType = "application/json"
                };

                client.PutObjectAsync(request).ConfigureAwait(false);
            }
        }
    }
}

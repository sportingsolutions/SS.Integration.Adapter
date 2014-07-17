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
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using SportingSolutions.Udapi.Sdk.Extensions;
using SS.Integration.Adapter.MarketRules.Model;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.ProcessState;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class ModelTests
    {

        [Test]
        public void SerializeModelTest()
        {

            Fixture fixture = new Fixture
            {
                FixtureName = "TEST-NAME",
                Epoch = 10,
                LastEpochChangeReason = new[] { 10, 20 },
                Id = "TEST-ID",
                StartTime = new DateTime(2000, 01, 01),
                Sequence = 12,
                MatchStatus = "40"
            };
            fixture.Tags.Add("TEST-TAG-1", "1");
            fixture.Tags.Add("TEST-TAG-2", 2);
            fixture.GameState.Add("TEST-STATE-1", 1);
            fixture.GameState.Add("TEST-STATE-2", "2");

            Participant p1 = new Participant { Id = 1, Name = "P1" };
            p1.Tags.Add("TEST-TAG-1", "1");
            p1.Tags.Add("TEST-TAG-2", 2);

            Participant p2 = new Participant { Id = 2, Name = "P2" };

            fixture.Participants.Add(p1);
            fixture.Participants.Add(p2);

            Market mkt1 = new Market { Id = "MKT1" };
            mkt1.AddOrUpdateTagValue("name", "MKT1");

            Selection seln1 = new Selection
            {
                Id = "SELN1",
                Price = 2.0,
                Status = SelectionStatus.Active,
                Tradable = false
            };
            seln1.AddOrUpdateTagValue("name", "seln1");
            seln1.AddOrUpdateTagValue("displayed", "true");

            Selection seln2 = new Selection
            {
                Id = "SELN2",
                Price = 3.0,
                Status = SelectionStatus.Pending,
                Tradable = true
            };
            seln2.AddOrUpdateTagValue("name", "seln2");
            seln2.AddOrUpdateTagValue("displayed", "false");

            mkt1.Selections.Add(seln1);
            mkt1.Selections.Add(seln2);

            RollingMarket mkt2 = new RollingMarket { Id = "RMKT2", Line = 2.0 };
            mkt2.AddOrUpdateTagValue("name", "RMKT2");

            RollingSelection seln3 = new RollingSelection
            {
                Id = "SELN3",
                Price = 7.0,
                Status = SelectionStatus.Pending,
                Tradable = true,
                Line = 21
            };

            ((Market)mkt2).Selections.Add(seln3);
            Market test2 = mkt2;
            test2.Selections.Count.Should().Be(1);
            (test2.Selections.FirstOrDefault() is RollingSelection).Should().BeTrue();

            Market mkt3 = new RollingMarket();
            mkt3.Id = "RMKT3";
            mkt3.AddOrUpdateTagValue("name", "RMKT3");

            fixture.Markets.Add(mkt1);
            fixture.Markets.Add(mkt2);
            fixture.Markets.Add(mkt3);

            fixture.RollingMarkets.FirstOrDefault(x => x.Id == "MKT1").Should().BeNull();
            fixture.RollingMarkets.FirstOrDefault(x => x.Id == "RMKT2").Should().NotBeNull();
            fixture.RollingMarkets.FirstOrDefault(x => x.Id == "RMKT3").Should().NotBeNull();

            fixture.StandardMarkets.FirstOrDefault(x => x.Id == "MKT1").Should().NotBeNull();
            fixture.StandardMarkets.FirstOrDefault(x => x.Id == "RMKT2").Should().BeNull();
            fixture.StandardMarkets.FirstOrDefault(x => x.Id == "RMKT3").Should().BeNull();

            string value = FixtureJsonHelper.ToJson(fixture);
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(value)))
            {

                stream.Position = 0;
                string value2 = Encoding.UTF8.GetString(stream.ToArray());

                value.Should().BeEquivalentTo(value2);

                fixture = FixtureJsonHelper.GetFromJson(value2);
            }

            fixture.RollingMarkets.Count().Should().Be(2);
            fixture.StandardMarkets.Count().Should().Be(1);
            fixture.Markets.Count().Should().Be(3);
            fixture.Tags.Count.Should().Be(2);
            fixture.GameState.Count().Should().Be(2);
            fixture.Participants.Count().Should().Be(2);

            fixture.Markets.FirstOrDefault(x => x.Id == "MKT1").Selections.Count.Should().Be(2);
            fixture.Markets.FirstOrDefault(x => x.Id == "RMKT2").Selections.Count.Should().Be(1);
            fixture.Markets.FirstOrDefault(x => x.Id == "RMKT3").Selections.Count.Should().Be(0);

            BinaryStoreProvider<Fixture> provider = new BinaryStoreProvider<Fixture>(".", "test-{0}.dat");

            provider.SetObject("TEST", fixture);

            fixture = provider.GetObject("TEST");

            fixture.RollingMarkets.Count().Should().Be(2);
            fixture.StandardMarkets.Count().Should().Be(1);
            fixture.Markets.Count().Should().Be(3);
            fixture.Tags.Count.Should().Be(2);
            fixture.GameState.Count().Should().Be(2);
            fixture.Participants.Count().Should().Be(2);

            fixture.Markets.FirstOrDefault(x => x.Id == "MKT1").Selections.Count.Should().Be(2);
            fixture.Markets.FirstOrDefault(x => x.Id == "RMKT2").Selections.Count.Should().Be(1);
            fixture.Markets.FirstOrDefault(x => x.Id == "RMKT3").Selections.Count.Should().Be(0);

            fixture = value.FromJson<Fixture>();

            fixture.RollingMarkets.Count().Should().Be(2);
            fixture.StandardMarkets.Count().Should().Be(1);
            fixture.Markets.Count().Should().Be(3);
            fixture.Tags.Count.Should().Be(2);
            fixture.GameState.Count().Should().Be(2);
            fixture.Participants.Count().Should().Be(2);

            fixture.Markets.FirstOrDefault(x => x.Id == "MKT1").Selections.Count.Should().Be(2);
            fixture.Markets.FirstOrDefault(x => x.Id == "RMKT2").Selections.Count.Should().Be(1);
            fixture.Markets.FirstOrDefault(x => x.Id == "RMKT3").Selections.Count.Should().Be(0);
        }

    }
}

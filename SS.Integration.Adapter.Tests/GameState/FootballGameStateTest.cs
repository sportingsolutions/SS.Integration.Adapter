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
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.GameState;

namespace SS.Integration.Adapter.Tests.GameState
{
    [TestFixture]
    public class FootballGameStateTest
    {
        private Fixture _fixture;

        [SetUp]
        public void CreateFixture()
        {
            _fixture = new Fixture()
            {
                Id = "TestFixtureId",
                FixtureName = "Testing fixture gamestate"
            };

            _fixture.GameState.Clear();
        }

        [Test]
        public void HomeAwayGoalsTest()
        {
            _fixture.GameState.Add("matchsummary", "3-2 90:00 2nd");
            var fixtureGameState = new FootballGameState(_fixture);

            fixtureGameState.HomeGoals.Should().Be(3);
            fixtureGameState.AwayGoals.Should().Be(2);
            fixtureGameState.TotalGoals.Should().Be(5);

        }

        [Test]
        public void HomeAwayGoalsDoubleDigitsTest()
        {
            _fixture.GameState.Add("matchsummary", "13-12 90:00 2nd");
            var fixtureGameState = new FootballGameState(_fixture);

            fixtureGameState.HomeGoals.Should().Be(13);
            fixtureGameState.AwayGoals.Should().Be(12);
            fixtureGameState.TotalGoals.Should().Be(25);
        }

        [Test]
        public void NullFixtureTest()
        {
            var test = new Action(() => new FootballGameState(null));
            test.ShouldThrow<ArgumentException>();
        }

        [Test]
        public void EmptyGameStateTest()
        {
            var test = new Action(() => new FootballGameState(_fixture));
            test.ShouldThrow<ArgumentException>();
        }
    }
}


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
using FluentAssertions;
using NUnit.Framework;
using SS.Integration.Adapter.Diagnostics.Model;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class FixtureOverviewTests
    {
        [Test]
        public void GetDeltaTest()
        {
            var fixtureOverview = new FixtureOverview("TestId");

            fixtureOverview.ListenerOverview.IsStreaming = false;

            fixtureOverview.ListenerOverview.IsStreaming = true;

            var delta = fixtureOverview.GetDelta();
            delta.Should().NotBeNull();
            delta.ListenerOverview.Should().NotBeNull();
            delta.ListenerOverview.IsStreaming.HasValue.Should().BeTrue();
            delta.Id.Should().Be(fixtureOverview.Id);

            //hasn't changed
            delta.ListenerOverview.IsDeleted.HasValue.Should().BeFalse();
        }

        [Test]
        public void GetDeltaHasNotChangedTest()
        {
            var fixtureOverview = new FixtureOverview("TestId");

            fixtureOverview.ListenerOverview.IsStreaming = true;

            //initial set up will also create a delta this call clears it
            fixtureOverview.GetDelta();

            fixtureOverview.ListenerOverview.IsStreaming = true;
            var delta = fixtureOverview.GetDelta();
            delta.Should().BeNull();
        }

        [Test]
        public void GetErrorTest()
        {
            var fixtureOverview = new FixtureOverview("TestId");

            fixtureOverview.LastError = new ErrorOverview
            {
                ErroredAt = DateTime.UtcNow,
                Exception = new NullReferenceException(),
                IsErrored = true,

            };

            var delta = fixtureOverview.GetDelta();
            delta.LastError.Should().NotBeNull();
            delta.LastError.Should().Be(fixtureOverview.LastError);

            fixtureOverview.ListenerOverview.IsErrored = false;

            delta = fixtureOverview.GetDelta();
            delta.LastError.Should().NotBeNull();
            delta.LastError.ResolvedAt.HasValue.Should().BeTrue();
        }
    }
}


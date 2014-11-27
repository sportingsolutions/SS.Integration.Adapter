using System;
using FluentAssertions;
using NUnit.Framework;
using SS.Integration.Adapter.Diagnostics.Model;

namespace SS.Integration.Adapter.Diagnostics.Testing
{
    [TestFixture]
    public class FixtureOverviewTest
    {
        [Test]
        public void GetDeltaTest()
        {
            var fixtureOverview = new FixtureOverview
            {
                Id = "TestId",
                IsStreaming = false
            };

            fixtureOverview.IsStreaming = true;

            var delta = fixtureOverview.GetDelta();
            delta.Should().NotBeNull();
            delta.IsStreaming.HasValue.Should().BeTrue();
            delta.Id.Should().Be(fixtureOverview.Id);

            //hasn't changed
            delta.IsDeleted.HasValue.Should().BeFalse();
        }

        [Test]
        public void GetDeltaHasNotChangedTest()
        {
            var fixtureOverview = new FixtureOverview
            {
                Id = "TestId",
                IsStreaming = true
            };

            //initial set up will also create a delta this call clears it
            fixtureOverview.GetDelta();

            fixtureOverview.IsStreaming = true;
            var delta = fixtureOverview.GetDelta();
            delta.Should().BeNull();
        }

        [Test]
        public void GetErrorTest()
        {
            var fixtureOverview = new FixtureOverview
            {
                Id = "TestId",
            };

            fixtureOverview.LastError = new ErrorOverview
            {
                ErroredAt = DateTime.UtcNow,
                Exception = new NullReferenceException(),
                IsErrored = true,

            };

            var delta = fixtureOverview.GetDelta();
            delta.LastError.Should().NotBeNull();
            delta.LastError.Should().Be(fixtureOverview.LastError);

            fixtureOverview.IsErrored = false;

            delta = fixtureOverview.GetDelta();
            delta.LastError.Should().NotBeNull();
            delta.LastError.ResolvedAt.HasValue.Should().BeTrue();

        }
    }
}

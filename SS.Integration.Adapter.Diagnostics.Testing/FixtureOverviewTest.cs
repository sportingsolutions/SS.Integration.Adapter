using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using SS.Integration.Adapter.Diagnostics.Model;

namespace SS.Integration.Adapter.Diagnostics.Testing
{
    [TestFixture]
    public class FixtureOverviewTest
    {
        [Test]
        public void OnChangeTest()
        {
            var fixtureOverview = new FixtureOverview
            {
                Id = "TestId",
                IsStreaming = false
            };

            fixtureOverview.IsStreaming = true;

            var changes = fixtureOverview.GetChanges();
            changes.Should().NotBeEmpty();
            changes.First(x => x.ItemName == "IsStreaming").Should().NotBeNull();
            changes.First(x => x.ItemName == "IsStreaming").PreviousValue.Should().Be(false.ToString());

        }
    }
}

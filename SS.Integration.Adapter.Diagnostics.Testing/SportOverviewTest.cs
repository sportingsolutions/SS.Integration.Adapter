using FluentAssertions;
using NUnit.Framework;
using SS.Integration.Adapter.Diagnostics.Model;

namespace SS.Integration.Adapter.Diagnostics.Testing
{
    [TestFixture]
    public class SportOverviewTest
    {
        [Test]
        public void EqualityTest()
        {
            //objects should be equal

            var sport = new SportOverview()
            {
                InErrorState = 3,
                InPlay = 4,
                InPreMatch = 5,
                InSetup = 7,
                Name = "Tennis",
                Total = 3 + 4 + 5
            };

            var secondSport = new SportOverview()
            {
                InErrorState = 3,
                InPlay = 4,
                InPreMatch = 5,
                InSetup = 7,
                Name = "Tennis",
                Total = 3 + 4 + 5
            };

            sport.Equals(secondSport).Should().BeTrue();
        }

        [Test]
        public void EqualityTestOnNonEqualObjectsTest()
        {
            //objects should be equal

            var sport = new SportOverview()
            {
                InErrorState = 30,
                InPlay = 4,
                InPreMatch = 5,
                InSetup = 7,
                Name = "Tennis",
                Total = 3 + 4 + 5
            };

            var secondSport = new SportOverview()
            {
                InErrorState = 3,
                InPlay = 4,
                InPreMatch = 5,
                Name = "Tennis",
                Total = 3 + 4 + 5
            };

            sport.Equals(secondSport).Should().BeFalse();
        }
    }
}

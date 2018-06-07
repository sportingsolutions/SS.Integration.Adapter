using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using SS.Integration.Common.Extensions;

namespace SS.Integration.Adapter.Tests
{
    [TestFixture]
    public class TimeExtensionsTest
    {

        [TestCase(1,1 ,1)]
        [TestCase(2, 1, 2)]
        [TestCase(3, 1, 4)]
        [TestCase(4, 1, 8)]
        [TestCase(3, 10, 40)]
        public void RetryIntervalTest(int retry, int startInterval, int expectedSeconds)
        {

            (startInterval > 1 ? retry.RetryInterval(startInterval) : retry.RetryInterval())
                .Seconds.Should().Be(expectedSeconds);
            //state.IsResulted.Should().Be(expected);
        }
    }
}

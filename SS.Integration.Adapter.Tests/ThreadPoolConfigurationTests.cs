//Copyright 2017 Spin Services Limited

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//    http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System.Threading;
using Moq;
using NUnit.Framework;
using SS.Integration.Adapter.Configuration;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Tests
{
    /// <summary>
    /// Tests for <see cref="ThreadPoolConfiguration"/>. The thread pool is process wide, so every test
    /// restores the minimum it found when it started.
    /// </summary>
    [TestFixture]
    public class ThreadPoolConfigurationTests
    {
        #region Constants

        public const string THREAD_POOL_CONFIGURATION_CATEGORY = nameof(ThreadPoolConfigurationTests);

        #endregion

        #region Fields

        private int _originalMinWorkerThreads;
        private int _originalMinCompletionPortThreads;
        private int _maxWorkerThreads;
        private int _maxCompletionPortThreads;

        #endregion

        #region SetUp

        [SetUp]
        public void SetupTest()
        {
            ThreadPool.GetMinThreads(out _originalMinWorkerThreads, out _originalMinCompletionPortThreads);
            ThreadPool.GetMaxThreads(out _maxWorkerThreads, out _maxCompletionPortThreads);
        }

        [TearDown]
        public void TearDownTest()
        {
            ThreadPool.SetMinThreads(_originalMinWorkerThreads, _originalMinCompletionPortThreads);
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// This test ensures the runtime default is kept when ThreadPoolMinThreads is not configured (0).
        /// </summary>
        [Test]
        [Category(THREAD_POOL_CONFIGURATION_CATEGORY)]
        public void GivenThreadPoolMinThreadsNotConfiguredThenTheMinimumIsUnchanged()
        {
            //
            //Arrange
            //
            var settings = SettingsWithThreadPoolMinThreads(0);

            //
            //Act
            //
            var applied = ThreadPoolConfiguration.Apply(settings);

            //
            //Assert
            //
            Assert.IsFalse(applied);
            AssertMinThreads(_originalMinWorkerThreads, _originalMinCompletionPortThreads);
        }

        /// <summary>
        /// This test ensures a configured minimum higher than the current one is applied to both the worker
        /// and the I/O completion threads.
        /// </summary>
        [Test]
        [Category(THREAD_POOL_CONFIGURATION_CATEGORY)]
        public void GivenThreadPoolMinThreadsHigherThanCurrentThenTheMinimumIsRaised()
        {
            //
            //Arrange
            //
            var requested = System.Math.Max(_originalMinWorkerThreads, _originalMinCompletionPortThreads) + 16;
            Assume.That(requested <= _maxWorkerThreads && requested <= _maxCompletionPortThreads,
                "thread pool maximum too low for this test");
            var settings = SettingsWithThreadPoolMinThreads(requested);

            //
            //Act
            //
            var applied = ThreadPoolConfiguration.Apply(settings);

            //
            //Assert
            //
            Assert.IsTrue(applied);
            AssertMinThreads(requested, requested);
        }

        /// <summary>
        /// This test ensures the minimum is never lowered by a configured value below the current one.
        /// </summary>
        [Test]
        [Category(THREAD_POOL_CONFIGURATION_CATEGORY)]
        public void GivenThreadPoolMinThreadsNotHigherThanCurrentThenTheMinimumIsUnchanged()
        {
            //
            //Arrange
            //
            var requested = System.Math.Min(_originalMinWorkerThreads, _originalMinCompletionPortThreads);
            var settings = SettingsWithThreadPoolMinThreads(requested);

            //
            //Act
            //
            var applied = ThreadPoolConfiguration.Apply(settings);

            //
            //Assert
            //
            Assert.IsFalse(applied);
            AssertMinThreads(_originalMinWorkerThreads, _originalMinCompletionPortThreads);
        }

        /// <summary>
        /// This test ensures a configured minimum above the thread pool maximum is rejected and logged
        /// rather than applied or thrown.
        /// </summary>
        [Test]
        [Category(THREAD_POOL_CONFIGURATION_CATEGORY)]
        public void GivenThreadPoolMinThreadsAboveMaximumThenTheMinimumIsUnchanged()
        {
            //
            //Arrange
            //
            var requested = System.Math.Max(_maxWorkerThreads, _maxCompletionPortThreads) + 1;
            var settings = SettingsWithThreadPoolMinThreads(requested);

            //
            //Act
            //
            var applied = ThreadPoolConfiguration.Apply(settings);

            //
            //Assert
            //
            Assert.IsFalse(applied);
            AssertMinThreads(_originalMinWorkerThreads, _originalMinCompletionPortThreads);
        }

        /// <summary>
        /// This test ensures the description logged at start-up carries the values needed to size the pool
        /// for a deployment (processor count and the pool limits).
        /// </summary>
        [Test]
        [Category(THREAD_POOL_CONFIGURATION_CATEGORY)]
        public void GivenDescribeThenItReportsProcessorCountAndPoolLimits()
        {
            //
            //Act
            //
            var description = ThreadPoolConfiguration.Describe();

            //
            //Assert
            //
            StringAssert.Contains($"processorCount={System.Environment.ProcessorCount}", description);
            StringAssert.Contains($"minWorkerThreads={_originalMinWorkerThreads}", description);
            StringAssert.Contains($"maxWorkerThreads={_maxWorkerThreads}", description);
        }

        #endregion

        #region Private methods

        private static ISettings SettingsWithThreadPoolMinThreads(int threadPoolMinThreads)
        {
            var settingsMock = new Mock<ISettings>();
            settingsMock.SetupGet(s => s.ThreadPoolMinThreads).Returns(threadPoolMinThreads);
            return settingsMock.Object;
        }

        private static void AssertMinThreads(int expectedWorkerThreads, int expectedCompletionPortThreads)
        {
            ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
            Assert.AreEqual(expectedWorkerThreads, workerThreads, "minimum worker threads");
            Assert.AreEqual(expectedCompletionPortThreads, completionPortThreads, "minimum completion port threads");
        }

        #endregion
    }
}

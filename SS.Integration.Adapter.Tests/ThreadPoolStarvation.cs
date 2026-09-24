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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace SS.Integration.Adapter.Tests
{
    /// <summary>
    /// Caps the .NET thread pool at the processor count and occupies every worker thread with blocked work,
    /// so nothing queued on the pool runs until disposed. Verifies the starvation with a probe before returning.
    /// Reproduces the kick-off surge condition for tests that assert an actor no longer depends on a pool thread.
    /// </summary>
    public sealed class ThreadPoolStarvation : IDisposable
    {
        private readonly int _originalMinWorker;
        private readonly int _originalMinIo;
        private readonly int _originalMaxWorker;
        private readonly int _originalMaxIo;
        private readonly ManualResetEventSlim _releaseBlockers = new ManualResetEventSlim(false);
        private readonly List<Task> _blockers = new List<Task>();

        private ThreadPoolStarvation()
        {
            ThreadPool.GetMinThreads(out _originalMinWorker, out _originalMinIo);
            ThreadPool.GetMaxThreads(out _originalMaxWorker, out _originalMaxIo);
        }

        public static ThreadPoolStarvation Start()
        {
            var starvation = new ThreadPoolStarvation();
            try
            {
                var poolSize = Environment.ProcessorCount;
                Assert.IsTrue(ThreadPool.SetMinThreads(poolSize, starvation._originalMinIo), "could not set min pool threads");
                Assert.IsTrue(ThreadPool.SetMaxThreads(poolSize, starvation._originalMaxIo), "could not set max pool threads");

                //occupy every worker thread the pool is allowed to have, plus a few queued behind them
                for (var i = 0; i < poolSize + 4; i++)
                {
                    starvation._blockers.Add(Task.Run(() => starvation._releaseBlockers.Wait()));
                }
                //sanity check: the pool really is starved, ordinary pool work does not get a thread
                var probe = Task.Run(() => true);
                Assert.IsFalse(probe.Wait(TimeSpan.FromMilliseconds(500)), "thread pool is not starved, test setup is invalid");

                return starvation;
            }
            catch
            {
                starvation.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            _releaseBlockers.Set();
            ThreadPool.SetMaxThreads(_originalMaxWorker, _originalMaxIo);
            ThreadPool.SetMinThreads(_originalMinWorker, _originalMinIo);
            Task.WaitAll(_blockers.ToArray(), TimeSpan.FromSeconds(5));
        }
    }
}

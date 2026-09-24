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
using System.Threading;
using log4net;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.Configuration
{
    /// <summary>
    /// Applies the adapter's .NET thread pool settings at start-up and logs the pool configuration the
    /// process actually runs with (processor count, minimum and maximum worker and I/O completion threads).
    /// 
    /// Everything in the adapter runs on the thread pool: the Akka actors, the SDK's stream consumers and
    /// echo checks, and the HTTP client. When many of them block at the same time (a kick-off surge) the
    /// pool only adds threads above its minimum at a slow rate, so even trivial work waits for a thread.
    /// Raising the minimum (ThreadPool.SetMinThreads) removes that injection delay; it does not remove the
    /// blocking itself.
    /// </summary>
    public static class ThreadPoolConfiguration
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ThreadPoolConfiguration));

        /// <summary>
        /// Logs the current thread pool configuration and, when <see cref="ISettings.ThreadPoolMinThreads"/> is
        /// greater than 0, raises the minimum number of worker and I/O completion threads to that value.
        /// The minimum is never lowered: a configured value below the current minimum is left as is.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns>true if the minimum was changed, false if it was left unchanged (not configured, not higher than the current minimum, or rejected by the runtime)</returns>
        public static bool Apply(ISettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            Logger.Info($"Thread pool before applying settings: {Describe()}");

            var requested = settings.ThreadPoolMinThreads;
            if (requested <= 0)
            {
                Logger.Info("ThreadPoolMinThreads not configured, keeping the runtime default thread pool minimum");
                return false;
            }

            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);
            ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);

            var workerThreads = Math.Max(minWorkerThreads, requested);
            var completionPortThreads = Math.Max(minCompletionPortThreads, requested);

            if (workerThreads == minWorkerThreads && completionPortThreads == minCompletionPortThreads)
            {
                Logger.Info(
                    $"ThreadPoolMinThreads={requested} is not higher than the current minimum (workerThreads={minWorkerThreads} completionPortThreads={minCompletionPortThreads}), leaving it unchanged");
                return false;
            }

            if (workerThreads > maxWorkerThreads || completionPortThreads > maxCompletionPortThreads)
            {
                Logger.Warn(
                    $"ThreadPoolMinThreads={requested} exceeds the thread pool maximum (maxWorkerThreads={maxWorkerThreads} maxCompletionPortThreads={maxCompletionPortThreads}), leaving the minimum unchanged");
                return false;
            }

            if (!ThreadPool.SetMinThreads(workerThreads, completionPortThreads))
            {
                Logger.Warn(
                    $"The runtime rejected ThreadPool.SetMinThreads(workerThreads={workerThreads}, completionPortThreads={completionPortThreads}), leaving the minimum unchanged. {Describe()}");
                return false;
            }

            Logger.Info($"Thread pool minimum raised (ThreadPoolMinThreads={requested}). {Describe()}");
            return true;
        }

        /// <summary>
        /// Returns a one line description of the thread pool the process runs with.
        /// </summary>
        /// <returns></returns>
        public static string Describe()
        {
            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);
            ThreadPool.GetMaxThreads(out var maxWorkerThreads, out var maxCompletionPortThreads);
            ThreadPool.GetAvailableThreads(out var availableWorkerThreads, out var availableCompletionPortThreads);

            return $"processorCount={Environment.ProcessorCount} " +
                   $"minWorkerThreads={minWorkerThreads} minCompletionPortThreads={minCompletionPortThreads} " +
                   $"maxWorkerThreads={maxWorkerThreads} maxCompletionPortThreads={maxCompletionPortThreads} " +
                   $"busyWorkerThreads={maxWorkerThreads - availableWorkerThreads} busyCompletionPortThreads={maxCompletionPortThreads - availableCompletionPortThreads} " +
                   $"runtime={Environment.Version} is64BitProcess={Environment.Is64BitProcess}";
        }
    }
}

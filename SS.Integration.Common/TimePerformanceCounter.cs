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
using System.Diagnostics;

namespace SS.Integration.Common
{
    /// <summary>
    /// Performance Counter that accounts for time elapsed for an executed request.
    /// </summary>
    public class TimePerformanceCounter : PerformanceCounterBase
    {
        private readonly string instanceName;
        private readonly Stopwatch stopWatch = new Stopwatch();

        static TimePerformanceCounter()
        {
            TimePerformanceCounter.SetUpCountersByCategory();
        }

        private TimePerformanceCounter(string instanceName)
        {
            this.instanceName = instanceName;
        }

        public static TimePerformanceCounter StartCounter(string instanceName)
        {
            var counter = new TimePerformanceCounter(instanceName);
            counter.StartWatch();

            return counter;
        }

        private void StartWatch()
        {
            this.stopWatch.Start();
        }

        public void StopCounter()
        {
            this.stopWatch.Stop();

            using (
                var averageExecutionTimeCounter = new PerformanceCounter(Category, 
                                                                         AverageExecutionTimeCounterName, 
                                                                         this.instanceName, 
                                                                         false))
            {
                using (
                    var averageExecutionTimeBaseCounter = new PerformanceCounter(Category, 
                                                                                 AverageExecutionTimeBaseCounterName, 
                                                                                 this.instanceName, 
                                                                                 false))
                {
                    averageExecutionTimeCounter.IncrementBy(this.stopWatch.ElapsedTicks);
                    averageExecutionTimeBaseCounter.Increment();
                }
            }
        }
    }
}

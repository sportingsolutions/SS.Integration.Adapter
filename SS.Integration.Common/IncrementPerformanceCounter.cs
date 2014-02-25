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
    /// Performance Counter that accounts for executed requests.
    /// It's got two performance counters: total number of requests and requests per second.
    /// </summary>
    public class IncrementPerformanceCounter : PerformanceCounterBase
    {
        static IncrementPerformanceCounter()
        {
            IncrementPerformanceCounter.SetUpCountersByCategory();
        }

        public static void IncreaseCounter(int numberOfExecutedRequests, string instanceName)
        {
            using (
                var processedRequestsPerSecondCounter = new PerformanceCounter(Category, 
                                                                               ProcessedRequestsPerSecondCounterName, 
                                                                               instanceName, 
                                                                               false))
            {
                processedRequestsPerSecondCounter.IncrementBy(numberOfExecutedRequests);
            }
        }

        public static void IncreaseCounter(string instanceName)
        {
            IncreaseCounter(1, instanceName);
        }
    }
}

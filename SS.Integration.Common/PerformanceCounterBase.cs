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
    /// Base class for Adapter Performance Counters
    /// </summary>
    public abstract class PerformanceCounterBase
    {
        protected static string Category
        {
            get { return "Integration Adapter"; }
        }

        protected static string ProcessedRequestsPerSecondCounterName
        {
            get { return "ProcessedRequestsPerSecond"; }
        }

        protected static string AverageExecutionTimeCounterName
        {
            get { return "AverageExecutionTime"; }
        }

        protected static string AverageExecutionTimeBaseCounterName
        {
            get { return "AverageExecutionTimeBase"; }
        }

        protected static void SetUpCountersByCategory()
        {
            if (!PerformanceCounterCategory.Exists(Category))
            {
                var counterDataCollection = new CounterCreationDataCollection();

                var executedRequestPerSecondCounter = new CounterCreationData();
                executedRequestPerSecondCounter.CounterType = PerformanceCounterType.RateOfCountsPerSecond32;
                executedRequestPerSecondCounter.CounterName = ProcessedRequestsPerSecondCounterName;
                executedRequestPerSecondCounter.CounterHelp = "Number of requests processed per second";
                counterDataCollection.Add(executedRequestPerSecondCounter);

                var averageExecutionTimeCounter = new CounterCreationData();
                averageExecutionTimeCounter.CounterType = PerformanceCounterType.AverageTimer32;
                averageExecutionTimeCounter.CounterName = AverageExecutionTimeCounterName;
                averageExecutionTimeCounter.CounterHelp = "Average Execution time per request";
                counterDataCollection.Add(averageExecutionTimeCounter);

                var averageExecutionTimeBaseCounter = new CounterCreationData();
                averageExecutionTimeBaseCounter.CounterType = PerformanceCounterType.AverageBase;
                averageExecutionTimeBaseCounter.CounterName = AverageExecutionTimeBaseCounterName;
                averageExecutionTimeBaseCounter.CounterHelp = "Average Execution time per request Base";
                counterDataCollection.Add(averageExecutionTimeBaseCounter);

                // Try to create the category
                try
                {
                    PerformanceCounterCategory.Create(Category, 
                                                      "Displays the various performance counters of Integration Adapter", 
                                                      PerformanceCounterCategoryType.MultiInstance, 
                                                      counterDataCollection);
                }
                catch (InvalidOperationException)
                {
                    // Ignore exception as the counters have just been created by another thread.
                }
            }
        }
    }
}

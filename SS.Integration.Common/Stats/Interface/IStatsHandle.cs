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


namespace SS.Integration.Common.Stats.Interface
{
    public interface IStatsHandle
    {
       
        /// <summary>
        /// Increment the value associated to the given key.
        /// </summary>
        /// <param name="key"></param>
        void IncrementValue(string key);

        /// <summary>
        /// Define a value for the given key.
        /// 
        /// This method should be used when the
        /// aim of the metric is to define
        /// a specific value for the key.
        /// In other words, the focus should be 
        /// on the definition of a value
        /// for the key rather than on how
        /// the values have changed
        /// during the time (for which AddValue
        /// is more appropriated).
        /// 
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void SetValue(string key, string value);

        /// <summary>
        /// Allows to specify a specific value associated
        /// to a key.
        /// 
        /// This method should be used when the focus
        /// is on how values for the key changed
        /// during the time
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void AddValue(string key, string value);

        /// <summary>
        /// As SetValue but may raise exceptions
        /// that have to be handled by the caller.
        /// Usefull on batches.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void SetValueUnsafe(string key, string value);

        /// <summary>
        /// As AddValue but may raise exceptions
        /// that have to be handled by the caller.
        /// Usefull on batches.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void AddValueUnsafe(string key, string value);

        /// <summary>
        /// As IncrementValue but may raise exceptions
        /// that have to be handled by the caller.
        /// Usefull on batches.
        /// </summary>
        /// <param name="key"></param>
        void IncrementValueUnsafe(string key);

    }
}

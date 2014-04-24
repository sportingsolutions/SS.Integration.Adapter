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
        /// This is a thread-safe method.
        /// If no value is associated with the given key, 
        /// then after the call, the associated value is 0.
        /// 
        /// Only integer increment is supported
        /// </summary>
        /// <param name="key"></param>
        void IncrementValue(string key);

        /// <summary>
        /// Decreases the value associated to the given key.
        /// This is a threa-safe method.
        /// If not value is associated with the given key,
        /// then after the call, the associated value is -1.
        /// </summary>
        /// <param name="key"></param>
        void DecrementValue(string key);

        /// <summary>
        /// Opposite of AddValue
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void RemoveValue(string key, object value);

        /// <summary>
        /// This is similar to SetValue, but 
        /// it appends a GlobalKeys.ADD_ITEM message
        /// before calling SetValue. 
        /// It is useful when the we want to store
        /// list of objects
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void AddValue(string key, object value);

        /// <summary>
        /// Allows to specify a specific value associated
        /// to a key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void SetValue(string key, object value);

        /// <summary>
        /// As SetValue but may throws exception
        /// that have to be handled by the caller.
        /// Usefull on batches.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void SetValueUnsafe(string key, object value);

        /// <summary>
        /// Allows to add a message before writing the item
        /// </summary>
        /// <param name="messagekey">A key to identify the message type</param>
        /// <param name="value">The actual message</param>
        /// <returns>This object, so to concatenate more messages in a easy way</returns>
        IStatsHandle AddMessage(string messagekey, object value);
    }
}

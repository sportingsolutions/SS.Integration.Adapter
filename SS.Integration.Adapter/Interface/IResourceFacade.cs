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
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SportingSolutions.Udapi.Sdk.Events;

namespace SS.Integration.Adapter.Interface
{
    public interface IResourceFacade
    {
        /// <summary>
        /// This event is called when the 
        /// stream for this resource is
        /// correctly established
        /// </summary>
        event EventHandler StreamConnected;

        /// <summary>
        /// This event is called when the stream
        /// for this fixture gets disconnected
        /// </summary>
        event EventHandler StreamDisconnected;

        /// <summary>
        /// This event is called everytime
        /// there is an event to be processed
        /// </summary>
        event EventHandler<StreamEventArgs> StreamEvent;

        /// <summary>
        /// The resource's Id
        /// </summary>
        string Id { get; }

        /// <summary>
        /// The resource's Name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The sport to which the resource
        /// belongs
        /// </summary>
        string Sport { get; }

        /// <summary>
        /// Returns true if the match 
        /// is over
        /// </summary>
        bool IsMatchOver { get; }

        /// <summary>
        /// Returns the resource's match status
        /// </summary>
        MatchStatus MatchStatus { get; }

        /// <summary>
        /// The resource's content
        /// </summary>
        Summary Content { get; }

        /// <summary>
        /// Allows to retrieve a snapshot
        /// for this resource.
        /// </summary>
        /// <returns>The snapshot in a raw JSON format</returns>
        string GetSnapshot();

        /// <summary>
        /// Starts the streaming connection for this resource.
        /// 
        /// It uses default echo interval and echo max delay values
        /// </summary>
        void StartStreaming();

        /// <summary>
        /// Starts the streaming connection for this resource.
        /// </summary>
        /// <param name="echoInterval"></param>
        /// <param name="echoMaxDelay"></param>
        void StartStreaming(int echoInterval, int echoMaxDelay);

        /// <summary>
        /// Allows to pause the streaming connection, meaning
        /// that no updates will be pushed through.
        /// </summary>
        void PauseStreaming();

        /// <summary>
        /// UnPause the streaming connection. See PauseStreaming()
        /// </summary>
        void UnPauseStreaming();

        /// <summary>
        /// Closes the streaming connection
        /// </summary>
        void StopStreaming();
    }
}

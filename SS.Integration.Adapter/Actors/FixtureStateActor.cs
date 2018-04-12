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
using System.Reflection;
using Akka.Actor;
using log4net;
using Newtonsoft.Json;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
    /// <summary>
    /// This actor class has the responsability to manage fixtures state.
    /// The state is kept in memory as dictionary and is automatically saved to disk by self scheduled message at pre-defined interval.
    /// </summary>
    public class FixtureStateActor : ReceiveActor
    {
        #region Constructors

        public const string ActorName = nameof(FixtureStateActor);
        public const string Path = "/user/" + ActorName;

        #endregion

        #region Fields

        private readonly ILog _logger = LogManager.GetLogger(typeof(FixtureStateActor));
        private readonly ISettings _settings;
        private readonly IStoreProvider _storeProvider;
        private string _pathFileName;
        private Dictionary<string, FixtureState> _fixturesState = new Dictionary<string, FixtureState>();

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="storeProvider"></param>
        public FixtureStateActor(ISettings settings, IStoreProvider storeProvider)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _storeProvider = storeProvider ?? throw new ArgumentNullException(nameof(storeProvider));

            SetFilePath();
            LoadStateFile();

            Context.System.Scheduler.ScheduleTellRepeatedly(
                _settings.FixturesStateAutoStoreInterval,
                _settings.FixturesStateAutoStoreInterval,
                Self,
                new WriteStateToFileMsg(),
                Self);

            Context.System.Scheduler.ScheduleTellRepeatedly(
                0,//run this the first time when adapter starts
                604800000,//run this every week
                Self,
                new CleanupStateFilesMsg(),
                Self);

            Receive<CheckFixtureStateMsg>(a => CheckFixtureStateMsgHandler(a));
            Receive<GetFixtureStateMsg>(a => GetFixtureStateMsgHandler(a));
            Receive<UpdateFixtureStateMsg>(a => UpdateFixtureStateMsgHandler(a));
            Receive<RemoveFixtureStateMsg>(a => RemoveFixtureStateMsgHandler(a));
            Receive<WriteStateToFileMsg>(a => WriteStateToFileMsgHandler(a));
            Receive<CleanupStateFilesMsg>(a => CleanupStateFilesMsgHandler(a));
        }

        #endregion

        #region Message handlers

        private void CheckFixtureStateMsgHandler(CheckFixtureStateMsg msg)
        {
            FixtureState fixtureState;
            _fixturesState.TryGetValue(msg.Resource.Id, out fixtureState);
            //if we don't have the fixture state stored then we should process it
            //otherwise if the match is not over then it means we should process it
            msg.ShouldProcessFixture = fixtureState == null || fixtureState.MatchStatus != MatchStatus.MatchOver;

            Sender.Tell(msg, Self);
        }

        private void GetFixtureStateMsgHandler(GetFixtureStateMsg msg)
        {
            FixtureState fixtureState;
            _fixturesState.TryGetValue(msg.FixtureId, out fixtureState);
            Sender.Tell(fixtureState, Self);
        }

        private void UpdateFixtureStateMsgHandler(UpdateFixtureStateMsg msg)
        {
            _logger.Debug($"Updating state for Fixture fixtureId={msg.FixtureId} sequence={msg.Sequence}");

            FixtureState fixtureState;
            if (!_fixturesState.TryGetValue(msg.FixtureId, out fixtureState))
            {
                fixtureState = new FixtureState { Id = msg.FixtureId, Sport = msg.Sport };
            }

            fixtureState.Sequence = msg.Sequence;
            fixtureState.MatchStatus = msg.Status;
            fixtureState.Epoch = msg.Epoch;

            _fixturesState[msg.FixtureId] = fixtureState;
        }

        private void RemoveFixtureStateMsgHandler(RemoveFixtureStateMsg msg)
        {
            if (_fixturesState.ContainsKey(msg.FixtureId))
            {
                _fixturesState.Remove(msg.FixtureId);
            }
        }

        private void WriteStateToFileMsgHandler(WriteStateToFileMsg msg)
        {
            try
            {
                var output = JsonConvert.SerializeObject(_fixturesState, Formatting.Indented);
                _storeProvider.Write(_pathFileName, output);
            }
            catch (Exception ex)
            {
                _logger.Error("Error when writting State to File", ex);
            }
        }

        private void CleanupStateFilesMsgHandler(CleanupStateFilesMsg msg)
        {
            _logger.Debug("Cleaning up state files");
            var stateProviderPath = System.IO.Path.IsPathRooted(_settings.StateProviderPath)
                ? _settings.StateProviderPath
                : System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    _settings.StateProviderPath);
            var stateProviderDir = new System.IO.DirectoryInfo(stateProviderPath);
            if (!stateProviderDir.Exists)
            {
                _logger.Debug($"Could not find stateProviderDir={stateProviderDir.FullName}");
                return;
            }

            var binFiles = stateProviderDir.GetFiles("*.bin", System.IO.SearchOption.AllDirectories);
            foreach (var binFile in binFiles)
            {
                if ((DateTime.UtcNow - binFile.LastWriteTimeUtc).TotalDays >= 7)
                {
                    _logger.Debug($"File {binFile.FullName} hasn't been updated in more than a week, going to remove it.");
                    binFile.Delete();
                }
            }
        }

        #endregion

        #region Protected methods

        protected override void PreRestart(Exception reason, object message)
        {
            _logger.Error(
                $"Actor restart reason exception={reason?.ToString() ?? "null"}." +
                (message != null
                    ? $" last processing messageType={message.GetType().Name}"
                    : ""));
            base.PreRestart(reason, message);
        }

        #endregion

        #region Private methods

        private void SetFilePath()
        {
            if (System.IO.Path.IsPathRooted(_settings.FixturesStateFilePath))
            {
                _pathFileName = _settings.FixturesStateFilePath;
            }
            else if (System.IO.Path.IsPathRooted(_settings.StateProviderPath))
            {
                _pathFileName = System.IO.Path.Combine(_settings.StateProviderPath, _settings.FixturesStateFilePath);
            }
            else
            {
                var path = Assembly.GetExecutingAssembly().Location;
                var fileInfo = new System.IO.FileInfo(path);
                var dir = fileInfo.DirectoryName;
                _pathFileName = System.IO.Path.Combine(
                    dir,
                    _settings.StateProviderPath,
                    _settings.FixturesStateFilePath);
            }
        }

        private void LoadStateFile()
        {
            try
            {
                _logger.InfoFormat("Attempting to load file from filePath {0}", _pathFileName);

                if (System.IO.File.Exists(_pathFileName))
                {
                    string savedFixtureStates = null;
                    try
                    {
                        savedFixtureStates = _storeProvider.Read(_pathFileName);
                    }
                    catch (Exception e)
                    {
                        _logger.Error($"LoadStateFile error reading file {_pathFileName} {e}");
                        throw;
                    }
                    

                    if (!string.IsNullOrWhiteSpace(savedFixtureStates))
                    {
                        if (JsonConvert.DeserializeObject(savedFixtureStates, typeof(Dictionary<string, FixtureState>))
                            is Dictionary<string, FixtureState> deserialized && deserialized != null)
                            _fixturesState = deserialized;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error($"Error during state deserialization {e}. The state file will be removed!");
                DeleteStateFile();
            }
        }

        private void DeleteStateFile()
        {
            if (System.IO.File.Exists(_pathFileName))
            {
                System.IO.File.Delete(_pathFileName);
                _logger.DebugFormat($"Deleted state file {_pathFileName}");
            }
        }

        #endregion

        #region Private messages

        private class WriteStateToFileMsg
        {
        }

        private class CleanupStateFilesMsg
        {
        }

        #endregion
    }
}
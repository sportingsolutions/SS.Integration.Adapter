using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Akka.Actor;
using log4net;
using Newtonsoft.Json;
using SS.Integration.Adapter.Actors.Messages;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Actors
{
    public class FixtureStateActor : ReceiveActor
    {
        #region Constructors

        public const string ActorName = nameof(FixtureStateActor);
        public const string Path = "/user/" + ActorName;

        #endregion

        #region Attributes

        private readonly ILog _logger = LogManager.GetLogger(typeof(FixtureStateActor));
        private readonly ISettings _settings;
        private readonly IStoreProvider _storeProvider;
        private string _pathFileName;
        private Dictionary<string, FixtureState> _fixturesStates;

        #endregion

        #region Constructors

        public FixtureStateActor(ISettings settings, IStoreProvider storeProvider)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _storeProvider = storeProvider ?? throw new ArgumentNullException(nameof(storeProvider));

            _fixturesStates = new Dictionary<string, FixtureState>();

            SetFilePath();
            LoadStateFile();

            Context.System.Scheduler.ScheduleTellRepeatedly(
                5000,
                5000,
                Self,
                new WriteStateToFileMsg(),
                Self);

            Receive<GetFixtureStateMsg>(a => GetFixtureStateMsgHandler(a));
            Receive<UpdateFixtureStateMsg>(a => UpdateFixtureStateMsgHandler(a));
            Receive<RemoveFixtureStateMsg>(a => RemoveFixtureStateMsgHandler(a));
            Receive<WriteStateToFileMsg>(a => WriteStateToFileMsgHandler(a));
        }

        #endregion

        #region Message handlers

        private void GetFixtureStateMsgHandler(GetFixtureStateMsg msg)
        {
            FixtureState fixtureState;
            _fixturesStates.TryGetValue(msg.FixtureId, out fixtureState);
            Sender.Tell(fixtureState, Self);
        }

        private void UpdateFixtureStateMsgHandler(UpdateFixtureStateMsg msg)
        {
            _logger.Debug($"Updating state for Fixture fixtureId={msg.FixtureId} sequence={msg.Sequence}");

            FixtureState fixtureState;
            if (!_fixturesStates.TryGetValue(msg.FixtureId, out fixtureState))
            {
                fixtureState = new FixtureState { Id = msg.FixtureId, Sport = msg.Sport };
            }

            fixtureState.Sequence = msg.Sequence;
            fixtureState.MatchStatus = msg.Status;
            fixtureState.Epoch = msg.Epoch;

            _fixturesStates[msg.FixtureId] = fixtureState;
        }

        private void RemoveFixtureStateMsgHandler(RemoveFixtureStateMsg msg)
        {
            _fixturesStates.Remove(msg.FixtureId);
        }

        private void WriteStateToFileMsgHandler(WriteStateToFileMsg msg)
        {
            try
            {
                lock (this)
                {
                    _logger.Debug($"Writing State to file, with {_fixturesStates.Count} fixtures");
                    var output = JsonConvert.SerializeObject(_fixturesStates, Formatting.Indented);
                    _storeProvider.Write(_pathFileName, output);
                    _logger.DebugFormat("State persisted successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error when writting State to File", ex);
            }
        }

        #endregion

        #region Private methods

        private void SetFilePath()
        {
            if (System.IO.Path.IsPathRooted(_settings.EventStateFilePath))
            {
                _pathFileName = _settings.EventStateFilePath;
            }
            else
            {
                var path = Assembly.GetExecutingAssembly().Location;
                var fileInfo = new FileInfo(path);
                var dir = fileInfo.DirectoryName;
                _pathFileName = System.IO.Path.Combine(dir, _settings.EventStateFilePath);
            }
        }

        private void LoadStateFile()
        {
            try
            {
                _logger.InfoFormat("Attempting to load file from filePath {0}", _pathFileName);

                var savedFixtureStates = File.Exists(_pathFileName) ? _storeProvider.Read(_pathFileName) : null;
                if (!string.IsNullOrWhiteSpace(savedFixtureStates))
                {
                    _fixturesStates = (Dictionary<string, FixtureState>)
                        JsonConvert.DeserializeObject(savedFixtureStates, typeof(Dictionary<string, FixtureState>));
                }
            }
            catch (JsonSerializationException jse)
            {
                _logger.Error($"Error during state deserialization {jse}. The state file will be removed!");
                DeleteStateFile();
            }
        }

        private void DeleteStateFile()
        {
            if (File.Exists(_pathFileName))
            {
                File.Delete(_pathFileName);
                _logger.DebugFormat($"Deleted state file {_pathFileName}");
            }
        }

        #endregion

        #region Private messages

        private class WriteStateToFileMsg
        {   
        }

        #endregion
    }
}
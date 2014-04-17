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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SS.Integration.Adapter.Interface;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter.Model.Enums;
using log4net;
using Newtonsoft.Json;


namespace SS.Integration.Adapter.ProcessState
{
    public class EventState : IEventState
    {
        private readonly IStoreProvider _storeProvider;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(EventState).ToString());

        private EventState(IStoreProvider storeProvider)
        {
            _storeProvider = storeProvider;
            Events = new ConcurrentDictionary<string, FixtureState>();
        }

        private ConcurrentDictionary<string, FixtureState> Events { get; set; }

        public static EventState Create(IStoreProvider storeProvider, ISettings settings)
        {
            var savedEvents = new ConcurrentDictionary<string, FixtureState>();

            SetFilePath(settings);

            try
            {
                _logger.InfoFormat("Attempting to load file from filePath {0}", PathFileName);

                var savedEventStates = storeProvider.Read(PathFileName);
                if (savedEventStates == null)
                {
                    savedEvents = new ConcurrentDictionary<string, FixtureState>();
                }
                else
                {
                    savedEvents = (ConcurrentDictionary<string, FixtureState>)
                                  JsonConvert.DeserializeObject(savedEventStates,
                                                                typeof(ConcurrentDictionary<string, FixtureState>));
                }

            }
            catch (FileNotFoundException)
            {
                savedEvents = new ConcurrentDictionary<string, FixtureState>();
            }
            catch (JsonSerializationException jse)
            {
                _logger.ErrorFormat("Error during state deserialization {0}. The state file will be removed!", jse);
                DeleteState();
            }

            var eventState = new EventState(storeProvider) { Events = savedEvents };

            return eventState;
        }

        private static void DeleteState()
        {
            _logger.DebugFormat("Deleting all event state");
            File.Delete(PathFileName);
        }

        private static void SetFilePath(ISettings settings)
        {
            if (Path.IsPathRooted(settings.EventStateFilePath))
            {
                PathFileName = settings.EventStateFilePath;
            }
            else
            {
                var path = Assembly.GetExecutingAssembly().Location;
                var fileInfo = new FileInfo(path);
                var dir = fileInfo.DirectoryName;
                PathFileName = Path.Combine(dir, settings.EventStateFilePath);
            }
        }

        public void WriteToFile()
        {
            try
            {
                lock (this)
                {
                    _logger.DebugFormat("Writing event state to file, with {0} events",Events.Count);
                    var output = JsonConvert.SerializeObject(Events, Formatting.Indented);
                    _storeProvider.Write(PathFileName, output);
                    _logger.DebugFormat("Event state persisted successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error when writting EventState", ex);
            }
        }

        public IEnumerable<string> GetFixtures(string sport)
        {
            return Events.Where(f => f.Value.Sport == sport).Select(f=> f.Value.Id);
        }

        public void RemoveInactiveFixtures(string sport, List<IResourceFacade> activeFixtures)
        {            
            var activeFixturesDic = activeFixtures.Where(f => !f.IsMatchOver).ToDictionary(f => f.Id);
            var removeFixtures = Events.Keys.Where(x => !activeFixturesDic.ContainsKey(x) && Events[x].Sport == sport);

            foreach (var inactiveFixture in removeFixtures)
            {
                var currentFixture = activeFixtures.FirstOrDefault(f => f.Id == inactiveFixture);
                if (currentFixture != null && currentFixture.MatchStatus == MatchStatus.MatchOver &&
                    Events[inactiveFixture].MatchStatus != MatchStatus.MatchOver)
                {
                    _logger.DebugFormat("Skipping event state clean up as fixture settlement wasn't processed");
                    continue;
                }

                FixtureState removedFixture;
                if (Events.TryRemove(inactiveFixture, out removedFixture))
                {
                    _logger.DebugFormat("Removing inactive fixture with fixtureId={0} from eventState",removedFixture.Id);
                }
            }
        }

        public void AddFixture(string sport, string fixtureId, int sequence)
        {
            _logger.DebugFormat("Updating state for Fixture fixtureId={0} sequence={1} sport={2}", fixtureId, sequence, sport);
            var fixtureState = GetFixtureState(fixtureId) ?? new FixtureState { Id = fixtureId, Sport = sport } ;

            fixtureState.Sequence = sequence;
            Events.AddOrUpdate(fixtureId, fixtureState, (key, oldValue) => fixtureState);
        }

        public void RemoveFixture(string sport, string fixtureId)
        {
            FixtureState abc;
            Events.TryRemove(fixtureId, out abc);
        }

        public void UpdateFixtureState(string sport, string fixtureId, int sequence, MatchStatus matchStatus)
        {
            _logger.DebugFormat("Updating state for Fixture fixtureId={0} sequence={1}", fixtureId, sequence);
            var fixtureState = GetFixtureState(fixtureId) ?? new FixtureState {Id = fixtureId, Sport = sport};

            fixtureState.Sequence = sequence;
            fixtureState.MatchStatus = matchStatus;

            Events.AddOrUpdate(fixtureId, fixtureState, (idX, fs) => fixtureState);
        }

        public int GetCurrentSequence(string sport, string fixtureId)
        {
            var currentSequence = -1;

            var fixtureState = GetFixtureState(fixtureId);


            return fixtureState != null ? fixtureState.Sequence : currentSequence;
        }

        public FixtureState GetFixtureState(string fixtureId)
        {
            FixtureState fixtureState = null;
            Events.TryGetValue(fixtureId, out fixtureState);
            return fixtureState;
        }

        private static string PathFileName { get; set; }
    }
}

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

using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;


namespace SS.Integration.Common.StatsAnalyser
{
    public class LogWatcher : IDisposable
    {
        private const int DEFAULT_BUFFER_SIZE = 2048;
        private const int DEFAULT_REFRESH_PERIOD = 1000;
        private const string MESSAGE_SEPARATOR = "- *MSGEND*";        

        private readonly List<string> _Files;
        private Thread _MainThread;        
        private FileSystemWatcher _Watcher;
        private JsonSerializerSettings _JsonSettings;
        private StreamReader _CurrentStream;
        private FileStream _CurrentFileStream;
        private string _NextLogFile;
        private string _CurrentLogFile;
        private StringBuilder _DataBuilder;

        public LogWatcher(string DirectoryPath)
        {
            if(string.IsNullOrEmpty(DirectoryPath) || !Directory.Exists(DirectoryPath))
                throw new ArgumentException("DirectoryPath doesn't exist", DirectoryPath);

            this.DirectoryPath = DirectoryPath;
            _Files = new List<string>();
        }

        public string DirectoryPath { get; private set; }

        private bool IsShuttingDown { get; set; }

        public void Start()
        {
            LoadData();
            InitialiseWatcher();
            _MainThread = new Thread(ReadLogFile);
            _MainThread.Start();
        }

        private void InitialiseWatcher()
        {
            if (_Watcher != null)
            {
                _Watcher.Dispose();
                _Watcher = null;
            }

            _Watcher = new FileSystemWatcher(DirectoryPath) 
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName
            };

            _Watcher.Created += Watcher_Created;
            _Watcher.Error += Watcher_Error;
        }

        private void LoadData()
        {
            MessageWatcherConfiguration.Instance.Dispatcher.SuspendNotifications();
            try
            {
                List<string> paths = Directory.EnumerateFiles(DirectoryPath).ToList();

                paths.Sort((x, y) => string.Compare(x, y, StringComparison.Ordinal));

                for (int i = 0; i < paths.Count - 1; i++)
                {
                    LoadData(paths[i]);
                }

                if(paths.Count > 0)
                    _CurrentLogFile = paths[paths.Count - 1];
            }
            finally
            {
                MessageWatcherConfiguration.Instance.Dispatcher.ResumeNotifications(false);
            }
        }

        private void LoadData(string filepath)
        {
            using (var fstream = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader reader = new StreamReader(fstream, Encoding.UTF8))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        line = line.Replace("- *MSGEND*", "");
                        ProcessSingleLine(line, false, true);
                    }
                }
            }
        }

        private void ProcessSingleLine(string line, bool newmessage, bool raiseexceptiononfail = false)
        {
            if (string.IsNullOrEmpty(line))
                return;

            if (_JsonSettings == null)
            {
                IsoDateTimeConverter converter = new IsoDateTimeConverter {DateTimeFormat = "yyyy-MM-dd HH:mm:ss,fff"};
                DefaultContractResolver dcr = new DefaultContractResolver();
                dcr.DefaultMembersSearchFlags |= BindingFlags.NonPublic;
                _JsonSettings = new JsonSerializerSettings {ContractResolver = dcr};
                _JsonSettings.Converters.Add(converter);
            }

            try
            {
                Message msg = JsonConvert.DeserializeObject(line, typeof(Message), _JsonSettings) as Message;
                MessageWatcherConfiguration.Instance.Dispatcher.DispatchMessage(msg, newmessage);                
            }
            catch
            {
                if (raiseexceptiononfail)
                    throw;
            }
        }

        private void ProduceData(string rawdata)
        {
            MessageWatcherConfiguration.Instance.Dispatcher.SuspendNotifications();

            try
            {
                string[] raw = rawdata.Split(new[] { MESSAGE_SEPARATOR }, StringSplitOptions.RemoveEmptyEntries);
                           
                foreach (string line in raw)
                {
                    var tmp = line.Replace("\n", "").Replace("\r", "").Trim();
                    if (string.IsNullOrEmpty(tmp))
                        continue;

                    ProcessSingleLine(tmp, true);
                }
            }
            finally 
            {
                MessageWatcherConfiguration.Instance.Dispatcher.ResumeNotifications(true);
            }            
        }

        private void ReadData()
        {
            string unprocesseddata = "";            

            string currentvalue = _CurrentStream.ReadToEnd();

            int index = currentvalue.LastIndexOf(MESSAGE_SEPARATOR, StringComparison.Ordinal);
            if (index > 0)
            {
                _DataBuilder.Append(currentvalue.Substring(0, index + MESSAGE_SEPARATOR.Length));
                unprocesseddata = currentvalue.Substring(index + MESSAGE_SEPARATOR.Length).Replace("\n", "").Replace("\r", "").Trim();

                ProduceData(_DataBuilder.ToString());
            }
            else
            {
                unprocesseddata = currentvalue;
            }

            if(!string.IsNullOrEmpty(unprocesseddata))
                _DataBuilder.Append(unprocesseddata);
        }

        private void ReadLogFile()
        {
            try
            {
                _DataBuilder = new StringBuilder();

                bool ctrl = true;
                bool innerctrl = true;
                string filepath = _CurrentLogFile;

                while (ctrl)
                {
                    if(!WaitForLogFile(out filepath))
                        break;

                    using (_CurrentFileStream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, DEFAULT_BUFFER_SIZE, FileOptions.SequentialScan))
                    {
                        using (_CurrentStream = new StreamReader(_CurrentFileStream, Encoding.UTF8, true, DEFAULT_BUFFER_SIZE, false))
                        {
                            while (innerctrl)
                            {
                                ReadData();

                                lock (this)
                                {
                                    if (!IsShuttingDown)
                                    {
                                        if (string.IsNullOrEmpty(_NextLogFile))
                                        {
                                            Monitor.Wait(this, DEFAULT_REFRESH_PERIOD);
                                        }
                                        else
                                        {
                                            innerctrl = false;
                                            filepath = _NextLogFile;
                                        }
                                    }

                                    if (IsShuttingDown)
                                    {
                                        innerctrl = false;
                                        ctrl = false;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private bool WaitForLogFile(out string Filepath)
        {
            bool ok = true;
            Filepath = _CurrentLogFile;

            if (string.IsNullOrEmpty(_CurrentLogFile))
            {
                lock (this)
                {
                    while (string.IsNullOrEmpty(_NextLogFile) && ok)
                    {
                        Monitor.Wait(this, DEFAULT_REFRESH_PERIOD);
                        ok = !IsShuttingDown;
                    }

                    Filepath = _NextLogFile;
                }
            }

            return ok;
        }

        private void Watcher_Error(object sender, System.IO.ErrorEventArgs e)
        {
            InitialiseWatcher();
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            lock (this)
            {
                // created events may be raised more than once for the same file
                if (_Files.Contains(e.FullPath))
                    return;

                _Files.Add(e.FullPath);

                lock (this)
                {
                    _NextLogFile = e.FullPath;
                    Monitor.Pulse(this);
                }
            }
        }

        public void Dispose()
        {
            if (_Watcher != null)
                _Watcher.Dispose();

            lock (this)
            {
                IsShuttingDown = true;

                if (_CurrentFileStream != null)
                    _CurrentFileStream.Close();

                Monitor.Pulse(this);
            }

            if(_MainThread != null)
                _MainThread.Join(DEFAULT_REFRESH_PERIOD);
        }
    }
}

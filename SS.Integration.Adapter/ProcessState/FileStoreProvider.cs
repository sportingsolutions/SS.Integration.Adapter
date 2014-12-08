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

using System.Reflection;
using System.IO;
using SS.Integration.Adapter.Interface;

namespace SS.Integration.Adapter.ProcessState
{
    public class FileStoreProvider : IStoreProvider
    {
        protected readonly string _directory;

        public FileStoreProvider(string directory = "FixturesStateFiles")
        {
            _directory = GetFullDirectoryPath(directory ?? "FixturesStateFiles");
            if (!Directory.Exists(_directory))
                Directory.CreateDirectory(_directory);
        }

        public string Read(string pathFileName)
        {
            pathFileName = GetFullPath(pathFileName);
            if (!File.Exists(pathFileName))
                return null;

            var output = string.Empty;

            using (var reader = new StreamReader(pathFileName))
            {
                output = reader.ReadToEnd();
            }

            return output;
        }
        
        public void Write(string pathFileName, string content)
        {
            pathFileName = GetFullPath(pathFileName);
            
            using (var writer = new StreamWriter(pathFileName,false))
            {
                writer.Write(content);
            }
        }

        private string GetFullDirectoryPath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(directory, path);
        }

        public string GetFullPath(string pathFileName)
        {
            if (Path.IsPathRooted(pathFileName))
                return pathFileName;

            return Path.Combine(_directory, pathFileName);
        }

    }
}

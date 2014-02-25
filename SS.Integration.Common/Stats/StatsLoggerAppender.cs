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
using System.IO;

namespace SS.Integration.Common.Stats
{
    public class StatsLoggerAppender : log4net.Appender.RollingFileAppender
    {
        private string BaseDirectory { get; set; }

        /// <summary>
        /// Given a base directory, a new directory will be created within it
        /// at each restart of the application. The new directories
        /// are named using "yyyyMMdd-X" date format where X
        /// is a counter for catering multiple restarts during the same day.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="append"></param>
        protected override void OpenFile(string fileName, bool append)
        {
            string filenameonly = Path.GetFileName(fileName);
            if (string.IsNullOrEmpty(BaseDirectory))
            {
                string basedirectory = Path.GetDirectoryName(fileName);

                if (!Directory.Exists(basedirectory))
                    Directory.CreateDirectory(basedirectory);

                string lastone = null;
                foreach (string path in Directory.EnumerateDirectories(basedirectory))
                {
                    if (lastone == null)
                        lastone = path;
                    else if (string.Compare(lastone, path, StringComparison.Ordinal) < 0)
                    {
                        lastone = path;
                    }
                }

                string current = DateTime.Now.ToString("yyyyMMdd");
                if (lastone == null || !Path.GetFileName(lastone).StartsWith(current))
                    current = current + "-1";
                else
                {
                    int index = lastone.IndexOf("-", StringComparison.Ordinal);
                    index = Convert.ToInt32(lastone.Substring(index + 1)) + 1;
                    current = current + "-" + index.ToString();
                }

                BaseDirectory = Path.Combine(basedirectory, current);
            }

            string newfilename = Path.Combine(BaseDirectory, filenameonly);

            base.OpenFile(newfilename, append);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SS.Integration.Adapter.Model;
using SS.Integration.Adapter;

namespace SS.Integration.Adapter.Helpers
{
    public static class FixtureLogger
    {
        private static string basePath = @"C:\Logs\Fixtures\";

        public static void Log(Fixture fixture)
        {
            var path = GetPath(fixture);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var sequence = fixture.Sequence;
            var filename = sequence + ".json";
            var fullFilename = Path.Combine(path, filename);

            if (!File.Exists(fullFilename))
            {
                var json = FixtureHelper.ToJson(fixture);
                System.IO.File.WriteAllText(fullFilename, json);
            }
        }
        
        private static string GetPath(Fixture fixture)
        {
            var today = DateTime.Today.ToShortDateString();
            return Path.Combine(basePath, today, fixture.Id);
        }
    }
}

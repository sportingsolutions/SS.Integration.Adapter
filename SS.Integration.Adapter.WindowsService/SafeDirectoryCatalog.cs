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
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SS.Integration.Adapter.WindowsService
{
    public class SafeDirectoryCatalog : ComposablePartCatalog
    {
        private readonly AggregateCatalog _catalog;

        public SafeDirectoryCatalog(string directoryPath, string assemblyName = "")
        {
            var files = Directory.EnumerateFiles(directoryPath, "*.dll", SearchOption.AllDirectories);

            _catalog = new AggregateCatalog();

            foreach (var file in files)
            {
                if (!string.IsNullOrEmpty(assemblyName) && !file.Contains(assemblyName + ".dll"))
                    continue;

                try
                {
                    var asmCat = new AssemblyCatalog(file);

                    //Force MEF to load the plugin and figure out if there are any exports
                    // good assemblies will not throw the RTLE exception and can be added to the catalog
                    _catalog.Catalogs.Add(asmCat);
                }
                catch (BadImageFormatException)
                {
                    Console.WriteLine("Ignoring file: " + file);
                }
                catch (ReflectionTypeLoadException)
                {
                    Console.WriteLine("Ignoring file: " + file);
                }
                catch (FileLoadException)
                {
                    Console.WriteLine("Ignoring file: " + file);
                }
            }
        }

        public override IQueryable<ComposablePartDefinition> Parts
        {
            get { return _catalog.Parts; }
        }
    }
}

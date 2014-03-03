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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Google.GData.Client;
using Google.GData.Spreadsheets;

namespace SS.Integration.Common.ConfigSerializer
{
    public class GoogleDocConfigSerializer : ISportConfigSerializer
    {

        #region Properties

        public GoogleDocSettings Settings { get; set; }

        #endregion

        #region Constructors


        public GoogleDocConfigSerializer()
        {

        }

        public GoogleDocConfigSerializer(GoogleDocSettings settings)
        {
            this.Settings = settings;
        }

        #endregion

        #region PrivateMethods

        private object GetDefaultValue(Type t)
        {
            if (t.IsValueType)
            {
                return Activator.CreateInstance(t);
            }

            return null;
        }

        private static SpreadsheetsService _service = null;
        private SpreadsheetsService Service
        {
            get
            {
                if (_service == null)
                {
                    _service = new SpreadsheetsService(this.Settings.AppName);
                    _service.setUserCredentials(this.Settings.Username, this.Settings.Password);
                }
                return _service;

            }

        }



        private SpreadsheetEntry GetSpreadSheetEntry(string fileName)
        {
            SpreadsheetQuery query = new SpreadsheetQuery();
            query.Title = fileName;
            query.Exact = true;
            SpreadsheetFeed feed = this.Service.Query(query);

            if (feed.Entries.Count == 0)
            {
                throw new FileNotFoundException();
            }

            return (SpreadsheetEntry)feed.Entries[0];
        }

        private WorksheetEntry CreateWorksheetByName(SpreadsheetEntry spreadsheet, string worksheetName)
        {
            WorksheetEntry worksheet = new WorksheetEntry();
            worksheet.Title.Text = worksheetName;
            WorksheetFeed wsFeed = spreadsheet.Worksheets;
            Service.Insert(wsFeed, worksheet);
            return worksheet;
        }

        private WorksheetEntry GetWorksheetByName(SpreadsheetEntry spreadsheet, string worksheetName)
        {
            AtomLink link = spreadsheet.Links.FindService(GDataSpreadsheetsNameTable.WorksheetRel, null);
            WorksheetQuery worksheetQuery = new WorksheetQuery(link.HRef.ToString());
            worksheetQuery.Exact = true;
            worksheetQuery.Title = worksheetName;
            WorksheetFeed worksheetFeed = Service.Query(worksheetQuery);
            if (worksheetFeed.Entries.Count == 0)
            {
                return null;
            }

            return (WorksheetEntry)worksheetFeed.Entries[0];
        }

        private ListFeed GetListFeed(WorksheetEntry worksheet)
        {
            // Define the URL to request the list feed of the worksheet.
            AtomLink listFeedLink = worksheet.Links.FindService(GDataSpreadsheetsNameTable.ListRel, null);

            // Fetch the list feed of the worksheet.
            ListQuery listQuery = new ListQuery(listFeedLink.HRef.ToString());
            ListFeed listFeed = this.Service.Query(listQuery);

            return listFeed;
        }

        private string SetFormattedStringFromRealvalue(double numberToFormat)
        {
            return numberToFormat.ToString();
        }

        private double GetRealValueFromFormattedString(string stringToParse)
        {
            return double.Parse(stringToParse);
        }

        private List<T> DeserializeInternal<T>(SpreadsheetEntry sse, WorksheetEntry we)
            where T : class, new()
        {
            List<T> result = new List<T>();
            Type type = typeof(T);

            // Iterate through each row, printing its cell values.
            AtomEntryCollection lstRows = GetListFeed(we).Entries;
            foreach (ListEntry row in lstRows)
            {
                T singleResult = new T();
                foreach (ListEntry.Custom element in row.Elements)
                {
                    PropertyInfo pi = type.GetProperty(element.LocalName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                    if (pi != null && element.Value != String.Empty && element.Value != null)
                    {
                        if (pi.PropertyType == typeof(Dictionary<string, string>))
                        {
                            pi.SetValue(singleResult, PopulateDictionary(element.Value));
                        }
                        else if (pi.PropertyType == typeof(double))
                        {
                            pi.SetValue(singleResult, GetRealValueFromFormattedString(element.Value));                            
                        }
                        else if (pi.PropertyType == typeof (string))
                        {
                            //IMPORTANT: Google Spreadsheet uses an hack to handle a SIMPLE number formatting (using the "+" in front a number).
                            //that kind of format is important for us (to map the handicap markets for example).
                            //the only solution to get this format in the spreadsheet is to put "' +" in front of the number 
                            //(for example, to display "+5", you have to write in the cell "' +5"). the problem comes out because the google data api
                            //reads that value as "' +5" rather than "+5". this forces me to get my hands dirty writing this.
                            string stringvalue = element.Value;
                            double tempN;
                            if (stringvalue.StartsWith("' +") && double.TryParse(stringvalue.Substring(3), out tempN))
                                pi.SetValue(singleResult, stringvalue.Substring(2), null);
                            else
                                pi.SetValue(singleResult, stringvalue, null);

                        }
                        else
                        {
                            pi.SetValue(singleResult, Convert.ChangeType(element.Value, pi.PropertyType), null);
                        }

                    }
                }
                result.Add(singleResult);
            }
            return result;
        }

        private string GetSerializedValue(object realValue,Type type)
        {
            if (type == typeof(Dictionary<string, string>))
                return GetStringFromDictionary((Dictionary<string, string>)realValue);
            if (type == typeof(double))
                return SetFormattedStringFromRealvalue((double)realValue);

            return realValue.ToString();
        }

        private void SerializeInternal<T>(List<T> settings, SpreadsheetEntry sse, WorksheetEntry we)
        {
            int settingsProcessed = 0;
            Type type = typeof(T);
            ListFeed listFeed = GetListFeed(we);

            foreach (ListEntry row in listFeed.Entries)
            {
                if (settingsProcessed < settings.Count)
                {

                    T singleElement = settings[settingsProcessed];
                    foreach (ListEntry.Custom element in row.Elements)
                    {
                        PropertyInfo pi = type.GetProperty(element.LocalName,
                                                           BindingFlags.IgnoreCase | BindingFlags.Public |
                                                           BindingFlags.Instance);

                        object realValue = pi.GetValue(settings[settingsProcessed]);

                        if (realValue != GetDefaultValue(pi.PropertyType))
                        {
                            element.Value = GetSerializedValue(realValue, pi.PropertyType);
                        }

                    }
                    settingsProcessed++;
                    row.Update();
                }
                else
                {
                    row.Delete();
                }
            }


            //insert rows until settingsProcessed == settings.Count
            while (settingsProcessed < settings.Count)
            {
                // Create a local representation of the new row.
                ListEntry row = new ListEntry();
                T singleElement = settings[settingsProcessed];

                //if is the first row inserted, we insert the headers too.
                if (settingsProcessed == 0)
                {
                    CellQuery cellQuery = new CellQuery(we.CellFeedLink);
                    CellFeed cellFeed = this.Service.Query(cellQuery);
                    PropertyInfo[] pinfos = type.GetProperties();
                    PropertyInfo pi;
                    for(int i = 0; i < pinfos.Count(); i++)
                    {
                        pi = pinfos[i];
                        CellEntry cellEntry = new CellEntry(1, (uint)(i+1), pi.Name.ToLower());
                        cellFeed.Insert(cellEntry);
                    }
                }

                foreach (PropertyInfo pi in type.GetProperties())
                {
                    var cell = new ListEntry.Custom()
                        {
                            LocalName = pi.Name.ToLower(),
                        };
                    object realValue = pi.GetValue(settings[settingsProcessed]);
                    if (realValue != GetDefaultValue(pi.PropertyType))
                    {
                        cell.Value = GetSerializedValue(realValue, pi.PropertyType);
                    }
                    row.Elements.Add(cell);
                }
                this.Service.Insert(listFeed, row);
                

                settingsProcessed++;
            }
        }

        private static Dictionary<string, string> PopulateDictionary(string tagNamesAndValues)
        {
            if (String.IsNullOrEmpty(tagNamesAndValues))
                return null;

            var dictionary = new Dictionary<string, string>();
            string[] singletag = tagNamesAndValues.Split(',');

            for (int i = 0; i < singletag.Length; i++)
            {
                string[] splittedTag = singletag[i].Split('=');
                dictionary.Add(splittedTag[0], splittedTag[1]);
            }

            return dictionary;
        }

        private static string GetStringFromDictionary(Dictionary<string, string> dict)
        {
            StringBuilder build = new StringBuilder();
            foreach (string singKey in dict.Keys)
            {
                build.Append(singKey);
                build.Append("=");
                build.Append(dict[singKey]);
                build.Append(",");
            }
            build.Length--;
            return build.ToString();
        }

        #endregion
         
        #region Implementation

        public List<T> Deserialize<T>(string fileNameOrReference) where T : class, new()
        {
            List<T> result = new List<T>();
            SpreadsheetEntry sse = GetSpreadSheetEntry(fileNameOrReference);
            WorksheetEntry we = (WorksheetEntry)sse.Worksheets.Entries[0];
            return DeserializeInternal<T>(sse, we);
        }

        public List<T> Deserialize<T>(string fileNameOrReference, string sportName)
            where T : class,new()
        {
            List<T> result = new List<T>();
            SpreadsheetEntry sse = GetSpreadSheetEntry(fileNameOrReference);
            WorksheetEntry we = GetWorksheetByName(sse, sportName);
            return DeserializeInternal<T>(sse, we);
        }

        public void Serialize<T>(List<T> settings, string fileNameOrReference)
        {
            SpreadsheetEntry sse = GetSpreadSheetEntry(fileNameOrReference);
            WorksheetEntry we = (WorksheetEntry)sse.Worksheets.Entries[0];
            SerializeInternal(settings, sse, we);
        }

        public void Serialize<T>(List<T> settings, string fileNameOrReference, string sportName)
        {
            SpreadsheetEntry sse = GetSpreadSheetEntry(fileNameOrReference);
            WorksheetEntry we = GetWorksheetByName(sse, sportName) ?? CreateWorksheetByName(sse, sportName);
            SerializeInternal(settings, sse, we);
        }


        public bool IsUpdateNeeded(string fileNameOrReference)
        {
            //we check in an additional worksheet called "Settings-NeedsUpdate" if the first cell is TRUE or FALSE.
            SpreadsheetEntry sse = GetSpreadSheetEntry(fileNameOrReference);
            WorksheetEntry we = GetWorksheetByName(sse, "Settings-NeedsUpdate");

            if (we == null)
                return false;

            ListFeed listFeed = GetListFeed(we);
            CellQuery cellQuery = new CellQuery(we.CellFeedLink);
            cellQuery.MinimumRow = 1;
            cellQuery.MaximumRow = 1;
            cellQuery.MinimumColumn = 1;
            cellQuery.MaximumColumn = 1;
            CellFeed cellFeed = Service.Query(cellQuery);

            if (cellFeed.Entries.Count == 0)
                return false;

            CellEntry cell = (CellEntry)cellFeed.Entries[0];

            bool result = bool.Parse(cell.Value);

            //updating again the cell to false so it doesn't update twice.
            cell.InputValue = false.ToString();
            cell.Update();

            return result;

        }

        public bool IsUpdateNeeded(string fileNameOrReference,string sportName)
        {
            //we check an additional field on the sport spreadsheet called "NeedsUpdate" if is TRUE or FALSE.
            SpreadsheetEntry sse = GetSpreadSheetEntry(fileNameOrReference);
            WorksheetEntry we = GetWorksheetByName(sse, sportName);
            

            AtomLink listFeedLink = we.Links.FindService(GDataSpreadsheetsNameTable.ListRel, null);

            // Fetch the list feed of the worksheet.
            ListQuery listQuery = new ListQuery(listFeedLink.HRef.ToString());

            ListFeed listFeed = this.Service.Query(listQuery);

            if (listFeed.Entries.Count == 0)
                return false;

            ListEntry.Custom cellResult = null;
            ListEntry firstRow = (ListEntry)listFeed.Entries[0];
            foreach (ListEntry.Custom element in firstRow.Elements)
            {
                if (element.XmlName.ToLower() == "needsupdate")
                    cellResult = element;
            }

            if (cellResult == null)
                return false;

            bool result = bool.Parse(cellResult.Value);
                 
            //we update that cell to false so it doesn't update twice.
            cellResult.Value = false.ToString();
            firstRow.Update();

            return result;
        }


        public string[] GetSportsList(string fileNameOrReference)
        {
            SpreadsheetEntry se = GetSpreadSheetEntry(fileNameOrReference);
            string[] result = se.Worksheets.Entries
                                .Where(we => !we.Title.Text.StartsWith("Settings-"))
                                .Select(we => we.Title.Text).ToArray();
            return result;
        }

        #endregion

    }
}

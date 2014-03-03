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
using System.Linq;
using Google.GData.Client;
using Google.GData.Spreadsheets;
using NUnit.Framework;

namespace SS.Integration.Common.ConfigSerializer.Tests
{
    [TestFixture]
    class GoogleDocConfigSerializerTest
    {

        private GoogleDocSettings _settings =
            new GoogleDocSettings
                {
                    Username = "ConnectIntegration@sportingsolutions.com",
                    Password = "Sp0rt!ng",
                    AppName = "UnitTests"
                };

        private class ClassToSerialize
        {
            public string ThisIsAString { get; set; }
            public Dictionary<string, string> ThisIsADictionary { get; set; }
            public bool ThisIsAFlag { get; set; }
            public int ThisIsANumber { get; set; }
            public double ThisIsADecimalNumber { get; set; }
        }

        private WorksheetEntry GetWorksheet(SpreadsheetsService service, string fileName, string worksheetName)
        {

            //get the spreadsheet object
            SpreadsheetQuery query = new SpreadsheetQuery();
            query.Title = fileName;
            query.Exact = true;
            SpreadsheetFeed feed = service.Query(query);
            SpreadsheetEntry sse = (SpreadsheetEntry)feed.Entries[0];

            //get the worksheet object
            AtomLink link = sse.Links.FindService(GDataSpreadsheetsNameTable.WorksheetRel, null);
            WorksheetQuery worksheetQuery = new WorksheetQuery(link.HRef.ToString());
            worksheetQuery.Title = worksheetName;
            worksheetQuery.Exact = true;
            WorksheetFeed worksheetFeed = service.Query(worksheetQuery);
            WorksheetEntry we = (WorksheetEntry)worksheetFeed.Entries[0];

            return we;
        }

        private void UpdateBackTheFlagToTrue(string fileName,string worksheetName)
        {
            //set the credentials
            SpreadsheetsService service = new SpreadsheetsService(_settings.AppName);
            service.setUserCredentials(_settings.Username, _settings.Password);
            WorksheetEntry we = GetWorksheet(service, fileName, worksheetName);

            AtomLink listFeedLink = we.Links.FindService(GDataSpreadsheetsNameTable.ListRel, null);

            ListQuery listQuery = new ListQuery(listFeedLink.HRef.ToString());
            ListFeed listFeed = service.Query(listQuery);

            if (listFeed.Entries.Count == 0)
                return;

            ListEntry.Custom cellResult = null;
            ListEntry firstRow = (ListEntry)listFeed.Entries[0];
            foreach (ListEntry.Custom element in firstRow.Elements)
            {
                if (element.XmlName.ToLower() == "needsupdate")
                    cellResult = element;
            }

            if (cellResult == null)
                return;

            cellResult.Value = true.ToString();
            firstRow.Update();

        }

        private void UpdateBackTheFlagToTrue(string fileName)
        {
            //set the credentials
            SpreadsheetsService service = new SpreadsheetsService(_settings.AppName);
            service.setUserCredentials(_settings.Username, _settings.Password);

            WorksheetEntry we = GetWorksheet(service, fileName, "Settings-NeedsUpdate");

            //get the list of cells
            AtomLink listFeedLink = we.Links.FindService(GDataSpreadsheetsNameTable.ListRel, null);
            ListQuery listQuery = new ListQuery(listFeedLink.HRef.ToString());
            ListFeed listFeed = service.Query(listQuery);

            //get the single cell
            CellQuery cellQuery = new CellQuery(we.CellFeedLink);
            cellQuery.MinimumRow = 1;
            cellQuery.MaximumRow = 1;
            cellQuery.MinimumColumn = 1;
            cellQuery.MaximumColumn = 1;
            CellFeed cellFeed = service.Query(cellQuery);
            CellEntry cell = (CellEntry)cellFeed.Entries[0];

            //update the value
            cell.InputValue = true.ToString();
            cell.Update();

        }

        [Test]
        public void UpdateFlagShouldBeTrueThenFalse()
        {
            //set first the designated flag to "TRUE"
            string fileName = "UnitTestSheet-UpdateFlagShouldBeTrueThenFalse"; 
            UpdateBackTheFlagToTrue(fileName);
            GoogleDocConfigSerializer ser = new GoogleDocConfigSerializer(_settings);
            Assert.IsTrue(ser.IsUpdateNeeded(fileName));
            Assert.IsFalse(ser.IsUpdateNeeded(fileName));
        }

        [Test]
        public void NeedsUpdateFlagPerSportTest()
        {
            string sport1 = "Baseball";
            string sport2 = "Cricket";
            string fileName = "UnitTestSheet-NeedsUpdateFlagPerSportTest"; 
            //update the flag back to TRUE in sport1. should return false at the second attempt.
            //the flag does not exists for sport2. "IsUpdateNeeded" should return false.
            UpdateBackTheFlagToTrue(fileName, sport1);
            
            GoogleDocConfigSerializer ser = new GoogleDocConfigSerializer(_settings);

            Assert.IsTrue(ser.IsUpdateNeeded(fileName, sport1));
            Assert.IsFalse(ser.IsUpdateNeeded(fileName, sport1));
            Assert.IsFalse(ser.IsUpdateNeeded(fileName, sport2));
        }


        [Test]
        public void SerializeAndDeserializeTest()
        {
            GoogleDocConfigSerializer ser = new GoogleDocConfigSerializer(_settings);
            ClassToSerialize instance;
            List<ClassToSerialize> listInstances = new List<ClassToSerialize>();

            instance = new ClassToSerialize();
            instance.ThisIsAString = "This Is A String";
            instance.ThisIsADecimalNumber = 0.12345;
            instance.ThisIsAFlag = true;
            instance.ThisIsANumber = 1985;
            instance.ThisIsADictionary = new Dictionary<string, string>();
            instance.ThisIsADictionary.Add("value1","2");
            instance.ThisIsADictionary.Add("value3", "4");
            instance.ThisIsADictionary.Add("value5", "6");
            listInstances.Add(instance);

            instance = new ClassToSerialize();
            instance.ThisIsAString = "This Is A second String";
            instance.ThisIsADecimalNumber = 0.54321;
            instance.ThisIsAFlag = false;
            instance.ThisIsANumber = 1986;
            instance.ThisIsADictionary = new Dictionary<string, string>();
            instance.ThisIsADictionary.Add("value2","1");
            instance.ThisIsADictionary.Add("value4", "3");
            instance.ThisIsADictionary.Add("value6", "5");
            listInstances.Add(instance);

            instance = new ClassToSerialize();
            instance.ThisIsAString = "This Is A Third String";
            instance.ThisIsADecimalNumber = 1.23;
            instance.ThisIsAFlag = false;
            instance.ThisIsANumber = 90;
            instance.ThisIsADictionary = null;
            listInstances.Add(instance);

            ser.Serialize(listInstances, "UnitTestSheet-SerializeAndDeserializeTest");
            List<ClassToSerialize> deserializedList = ser.Deserialize<ClassToSerialize>("UnitTestSheet-SerializeAndDeserializeTest");

            Func<ClassToSerialize, ClassToSerialize, bool> EqualityCheck = (x, y) =>
                {
                    return
                        String.Equals(x.ThisIsAString,y.ThisIsAString) &&
                        x.ThisIsAFlag == y.ThisIsAFlag &&
                        x.ThisIsANumber == y.ThisIsANumber &&
                        x.ThisIsADecimalNumber == y.ThisIsADecimalNumber &&
                        (
                            (x.ThisIsADictionary == null && y.ThisIsADictionary == null) 
                            ||
                            (
                                x.ThisIsADictionary.Count == y.ThisIsADictionary.Count &&
                                !x.ThisIsADictionary.Except(y.ThisIsADictionary).Any()
                            )
                        );
                };

            Assert.AreEqual(listInstances.Count, deserializedList.Count);

            for (int i = 0; i < listInstances.Count; i++)
            {
                Assert.IsTrue(EqualityCheck(listInstances[i],deserializedList[i]));
            }

        }


    }
}

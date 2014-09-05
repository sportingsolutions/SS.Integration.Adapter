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
using SS.Integration.Adapter.Model;

namespace SS.Integration.Adapter.Plugin.Model
{
    [Serializable]
    public class IndexMapping
    {
        private string _nextMarketId;

        public string MarketId { get; set; }

        public int Index { get; set; }
        public bool IsCurrent { get; set; }
        public string MarketName { get; set; }

        private readonly Dictionary<string, string> _selections = new Dictionary<string, string>();

        // don't use it's for serializer only
        public IndexMapping()
        {

        }

        public IndexMapping(Market firstMarket, Market firstActiveMarket, Market currentMarketIndex, int index, string nextMarketId, string selectionIndexTag, string marketName)
        {
            Index = index;
            MarketId = firstMarket.Id;
            NextMarketId = nextMarketId;
            MarketName = marketName;

            if (firstActiveMarket.Id == currentMarketIndex.Id)
                IsCurrent = true;

            SetSelectionIds(firstMarket, currentMarketIndex, selectionIndexTag);
        }

        private void SetSelectionIds(Market firstMarket, Market currentMarketIndex, string selectionIndexTag)
        {
            foreach (var selection in currentMarketIndex.Selections)
            {
                // tag selection matching by tags
                var matchingSelection = firstMarket.Selections.First(s => s.Tags.All(tag => TagEquals(selectionIndexTag, tag, selection)));

                _selections[selection.Id] = matchingSelection.Id;
            }
        }

        private bool TagEquals(string selectionIndexTag, KeyValuePair<string, string> tag, Selection selection)
        {
            var output = 
                //ignore selectiongroupid   
                tag.Key == "selectiongroupid" 
                //ignore index tag
                   || tag.Key == selectionIndexTag 
                //do comparison on all other tags (when they are passed in)
                   || (selection.Tags.ContainsKey(tag.Key) && string.Equals(selection.Tags[tag.Key], tag.Value, StringComparison.OrdinalIgnoreCase));

            return output;
        }


        public string GetIndexSelectionId(string selectionId)
        {
            return _selections.ContainsKey(selectionId) ? _selections[selectionId] : selectionId;
        }

        public string NextMarketId
        {
            get { return _nextMarketId; }
            set { _nextMarketId = value; }
        }

    }
}

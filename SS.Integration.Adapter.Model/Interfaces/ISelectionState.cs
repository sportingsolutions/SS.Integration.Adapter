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

using System.Collections.Generic;

namespace SS.Integration.Adapter.Model.Interfaces
{
    /// <summary>
    /// An ISelectionState represents the state of a selection.
    /// 
    /// At every snapshot (delta or full) the state of a selection
    /// is updated. This allows an IMarketState object to infer properties
    /// about a market.
    /// </summary>
    public interface ISelectionState
    {
        /// <summary>
        /// The selection's Id
        /// </summary>
        string Id { get; }

        /// <summary>
        /// The selection's status.
        /// 
        /// See SelectionStatus for a list of possibile values.
        /// </summary>
        string Status { get; }

        /// <summary>
        /// Selection's name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns the selection's tradability.
        /// </summary>
        bool? Tradability { get; }

        /// <summary>
        /// Returns the selection's price
        /// </summary>
        double? Price { get; }

        double? Line { get; }

        #region Specific fields for racing fixtures

        ISelectionResultState Result { get; }

        ISelectionResultState PlaceResult { get; }

        #endregion

        #region Tags

        /// <summary>
        /// Lists all the tags that a selection has had
        /// during its lifetime
        /// </summary>
        IEnumerable<string> TagKeys { get; }

        /// <summary>
        /// Get the value associated to the
        /// given tag key. 
        /// 
        /// It returns null if the tag key
        /// does not exist
        /// </summary>
        /// <param name="TagKey"></param>
        /// <returns></returns>
        string GetTagValue(string TagKey);

        /// <summary>
        /// Returns the number of tag associated
        /// to this selection state.
        /// </summary>
        int TagsCount { get; }

        /// <summary>
        /// True if the given tag key exists
        /// </summary>
        /// <param name="TagKey"></param>
        /// <returns></returns>
        bool HasTag(string TagKey);

        #endregion

        /// <summary>
        /// Determines if the given ISelectionState
        /// is equal to the current object
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        bool IsEqualTo(ISelectionState state);

        /// <summary>
        /// Determines if the given Selection object
        /// is equivalent to the current ISelectionState
        /// object
        /// </summary>
        /// <param name="selection"></param>
        /// <param name="checkTags">If true, tags are also checked</param>
        /// <returns></returns>
        bool IsEquivalentTo(Selection selection, bool checkTags);
    }
}

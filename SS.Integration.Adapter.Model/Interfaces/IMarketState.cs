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
    /// An IMarketState object represents the state of a market.
    /// The main purpose of keeping the state of a market 
    /// is to infer properties like "IsActive", "IsSuspended", "IsResulted", "IsPending".
    /// 
    /// As these properties can only be inferred by looking at the selections' states,
    /// it is difficult, if not impossibile, to determine a market's state 
    /// on a delta snapshot, as information about selections might be absent.
    /// 
    /// IMarketState keeps track of information coming from subsequents snapshots (full, or delta)
    /// updating correcly the above properties and then exposing them.
    /// 
    /// IMarketRule must infer the values of its properties by looking a the
    /// selections' states, or in other words, by looking at ISelectionState objects.
    /// </summary>
    public interface IMarketState
    {

        /// <summary>
        /// Market's Id
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Market's name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns true if the market is in a "Suspended" state.
        /// 
        /// This property only makes sense if IsActive is true as 
        /// a "Suspended" state doesn't have any meaning when the market
        /// is not active.
        /// </summary>
        bool IsSuspended { get; }

        /// <summary>
        /// Returns true if the market is active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Is the market resulted?
        /// This value returns the resulted state of
        /// a market as it comes from the Connect platform.
        /// 
        /// It is not related in any way to the resulted
        /// stated of any down-stream sytems.
        /// </summary>
        bool IsResulted { get; }

        /// <summary>
        /// Determines if all the selecions within
        /// this market have been voided.
        /// </summary>
        bool IsVoided { get; }

        /// <summary>
        /// Is the market in a pending state?
        /// 
        /// A pending state means that the market
        /// is in a transition state between two valid states
        /// (i.e Active and Resulted, ...).
        /// 
        /// Usually, when the market is an pending state, 
        /// no decision about it should be taken
        /// </summary>
        bool IsPending { get; }

        /// <summary>
        /// True if the market has been active at least once 
        /// during the fixture's lifetime
        /// </summary>
        bool HasBeenActive { get; }

        /// <summary>
        /// True if the market has been forcely
        /// suspended by the adapter.
        /// 
        /// This is different from IsSuspended
        /// which only looks at the tradability
        /// of the market
        /// </summary>
        bool IsForcedSuspended { get; }

        /// <summary>
        /// Returns the line of the market.
        /// Only valid if the market is an
        /// handicap/rolling market
        /// </summary>
        double? Line { get; }

        bool IsDeleted { get; set; }

        #region Selections 

        /// <summary>
        /// Lists all the ISelectionState objects
        /// </summary>
        IEnumerable<ISelectionState> Selections { get; }

        /// <summary>
        /// Returns the ISelectionState object associated
        /// to the given selection's id.
        /// 
        /// It returns null if the selection's id is
        /// null or empty of if the selection does not
        /// exist
        /// </summary>
        /// <param name="selectionId"></param>
        /// <returns></returns>
        ISelectionState this[string selectionId] { get; }

        /// <summary>
        /// True if an ISelectionState object for
        /// the given selection's id is present
        /// </summary>
        /// <param name="selectionId"></param>
        /// <returns></returns>
        bool HasSelection(string selectionId);

        /// <summary>
        /// Returns the number of selection in this market
        /// </summary>
        int SelectionsCount { get; }

        #endregion

        #region Tags

        /// <summary>
        /// Lists all the tags that a market has had
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
        /// True if the given tag key exists
        /// </summary>
        /// <param name="TagKey"></param>
        /// <returns></returns>
        bool HasTag(string TagKey);

        /// <summary>
        /// Returns the number of tag associated
        /// to this market state.
        /// </summary>
        int TagsCount { get; }

        #endregion

        /// <summary>
        /// Determines if the given market state
        /// is equal to the current object
        /// </summary>
        /// <param name="newMarket"></param>
        /// <returns></returns>
        bool IsEqualTo(IMarketState newMarket);

        /// <summary>
        /// Determines if the given Market object
        /// is equivalent to the current IMarketState
        /// object.
        /// </summary>
        /// <param name="market"></param>
        /// <param name="checkTags">If true, tags are checked too</param>
        /// <param name="checkSelections">If true, the method also checks
        /// equivalence for the selections</param>
        /// <returns></returns>
        bool IsEquivalentTo(Market market, bool checkTags, bool checkSelections);

        /// <summary>
        /// Allows to set force suspension state on a market.
        /// </summary>
        void SetForcedSuspensionState(bool isSuspended = true);
    }
}

using System;
using System.Collections.Generic;
using SS.Integration.Adapter.Model.Enums;
using ModelInterface = SS.Integration.Adapter.Diagnostics.Model.Interface;
using ServiceModelInterface = SS.Integration.Adapter.Diagnostics.Model.Service.Model.Interface;
using ServiceModel = SS.Integration.Adapter.Diagnostics.Model.Service.Model;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public static class ModelAdapter
    {
        private static readonly Dictionary<MatchStatus, ServiceModelInterface.FixtureState> MatchStatusToFixtureStateMapping =
            new Dictionary<MatchStatus, ServiceModelInterface.FixtureState>
            {
                { MatchStatus.NotApplicable, ServiceModelInterface.FixtureState.Setup },
                { MatchStatus.Deleted, ServiceModelInterface.FixtureState.Deleted },
                { MatchStatus.Setup, ServiceModelInterface.FixtureState.Setup },
                { MatchStatus.Ready, ServiceModelInterface.FixtureState.Ready },
                { MatchStatus.Prematch, ServiceModelInterface.FixtureState.PreMatch },
                { MatchStatus.ShowPrices, ServiceModelInterface.FixtureState.Setup },
                { MatchStatus.InRunning, ServiceModelInterface.FixtureState.Running },
                { MatchStatus.MatchOverUnConfirmed, ServiceModelInterface.FixtureState.Over },
                { MatchStatus.MatchOverConfirmedResults, ServiceModelInterface.FixtureState.Over },
                { MatchStatus.MatchOver, ServiceModelInterface.FixtureState.Over },
                { MatchStatus.Stopped, ServiceModelInterface.FixtureState.Over },
                { MatchStatus.Abandoned, ServiceModelInterface.FixtureState.Over },
                { MatchStatus.Paused, ServiceModelInterface.FixtureState.Ready }
            };

        public static T ToServiceModel<T>(this ModelInterface.ISportOverview model)
            where T : ServiceModel.SportOverview, new()
        {
            return new T
            {
                Name = model.Name,
                InErrorState = model.InErrorState,
                InPlay = model.InPlay,
                InPreMatch = model.InPreMatch,
                InSetup = model.InSetup,
                Total = model.Total
            };
        }

        public static T ToServiceModel<T>(this ModelInterface.IFixtureOverview model)
            where T : ServiceModel.FixtureOverview, new()
        {
            return new T
            {
                Id = model.Id,
                IsStreaming = model.ListenerOverview != null && model.ListenerOverview.IsStreaming.GetValueOrDefault(),
                IsInErrorState = model.ListenerOverview != null && model.ListenerOverview.IsErrored.GetValueOrDefault(),
                StartTime = model.ListenerOverview != null
                    ? model.ListenerOverview.StartTime.GetValueOrDefault()
                    : DateTime.MinValue,
                Competition = model.CompetitionName,
                CompetitionId = model.CompetitionId,
                Description = model.Name,
                State = model.ListenerOverview != null
                    ? MatchStatusToFixtureStateMapping.ContainsKey(model.ListenerOverview.MatchStatus.GetValueOrDefault())
                        ? MatchStatusToFixtureStateMapping[model.ListenerOverview.MatchStatus.GetValueOrDefault()]
                        : ServiceModelInterface.FixtureState.Setup
                    : ServiceModelInterface.FixtureState.Setup
            };
        }

        public static ServiceModel.SportDetails ToServiceModel(this ModelInterface.ISportOverview model)
        {
            return ToServiceModel<ServiceModel.SportDetails>(model);
        }

        public static ServiceModel.FixtureDetails ToServiceModel(this ModelInterface.IFixtureOverview model)
        {
            return ToServiceModel<ServiceModel.FixtureDetails>(model);
        }

        public static ServiceModel.FixtureProcessingEntry ToFixtureProcessingEntryServiceModel(this FeedUpdateOverview model)
        {
            return new ServiceModel.FixtureProcessingEntry
            {
                Epoch = model.Epoch.ToString(),
                EpochChangeReasons = model.LastEpochChangeReason,
                Exception = model.LastError,
                IsUpdate = !model.IsSnapshot,
                Sequence = model.Sequence.ToString(),
                Timestamp = model.Issued,
                State = model.IsProcessed
                    ? ServiceModelInterface.FixtureProcessingState.PROCESSED
                    : ServiceModelInterface.FixtureProcessingState.PROCESSING
            };
        }
    }
}

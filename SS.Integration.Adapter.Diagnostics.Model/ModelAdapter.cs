using ModelInterface = SS.Integration.Adapter.Diagnostics.Model.Interface;
using ServiceModel = SS.Integration.Adapter.Diagnostics.Model.Service.Model;

namespace SS.Integration.Adapter.Diagnostics.Model
{
    public static class ModelAdapter
    {
        public static ServiceModel.SportDetails ToServiceModel(this ModelInterface.ISportOverview model)
        {
            return new ServiceModel.SportDetails
            {
                Name = model.Name,
                InErrorState = model.InErrorState,
                InPlay = model.InPlay,
                InPreMatch = model.InPreMatch,
                InSetup = model.InSetup,
                Total = model.Total
            };
        }
    }
}

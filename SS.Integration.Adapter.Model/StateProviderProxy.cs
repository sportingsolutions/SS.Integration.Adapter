using SS.Integration.Adapter.Model.Interfaces;

namespace SS.Integration.Adapter.Model
{
    public class StateProviderProxy
    {
        public static bool IsInitialized => StateProviderProxy.StateProvider != null;

        public static IStateProvider StateProvider { get; private set; }

        internal static void Init(IStateProvider provider)
        {
            if (provider == null)
                return;
            StateProviderProxy.StateProvider = provider;
        }
    }
}

using System;

namespace SS.Integration.Adapter.Exceptions
{
    internal class PluginException : Exception
    {
        #region Constructors

        public PluginException()
            : base()
        {
        }

        public PluginException(string message)
            : base(message)
        {
        }

        public PluginException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }
}

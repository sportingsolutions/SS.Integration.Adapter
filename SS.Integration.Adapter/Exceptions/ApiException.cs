using System;

namespace SS.Integration.Adapter.Exceptions
{
    internal class ApiException : Exception
    {
        #region Constructors

        public ApiException()
            : base()
        {
        }

        public ApiException(string message)
            : base(message)
        {
        }

        public ApiException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }
}

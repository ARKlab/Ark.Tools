using System;

namespace Ark.Tools.Core
{
    public class OptimisticConcurrencyException : ApplicationException
    {
        public OptimisticConcurrencyException(string message)
            : base(message)
        {
        }

        public OptimisticConcurrencyException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }


        public OptimisticConcurrencyException(Exception inner, string message)
            : base(message, inner)
        {
        }

        public OptimisticConcurrencyException(Exception inner, string format, params object[] args)
            : base(string.Format(format, args), inner)
        {
        }

    }
}

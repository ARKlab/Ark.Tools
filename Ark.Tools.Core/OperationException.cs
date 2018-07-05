using System;

namespace Ark.Tools.Core
{
    public class OperationException : ApplicationException
    {
        public OperationException(string message)
            : base(message)
        {
        }

        public OperationException(string format, params object[] args)
            : base(string.Format(format, args))
        {
        }


        public OperationException(Exception inner, string message)
            : base(message, inner)
        {
        }

        public OperationException(Exception inner, string format, params object[] args)
            : base(string.Format(format, args), inner)
        {
        }

    }
}

using System;
using System.Globalization;

namespace Ark.Reference.Common.Exceptions
{
	public class ExternalServiceException : Exception
	{
		public ExternalServiceException(string message)
			: base(message)
		{
		}

		public ExternalServiceException(string format, params object[] args)
			: base(string.Format(CultureInfo.InvariantCulture, format, args))
		{
		}


		public ExternalServiceException(Exception inner, string message)
			: base(message, inner)
		{
		}

		public ExternalServiceException(Exception inner, string format, params object[] args)
			: base(string.Format(CultureInfo.InvariantCulture, format, args), inner)
		{
		}

	}
}

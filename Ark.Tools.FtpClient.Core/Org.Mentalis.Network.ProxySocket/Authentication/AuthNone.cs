// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Net.Sockets;

namespace Org.Mentalis.Network.ProxySocket.Authentication
{
    /// <summary>
    /// This class implements the 'No Authentication' scheme.
    /// </summary>
    internal sealed class AuthNone : AuthMethod {
		/// <summary>
		/// Initializes an AuthNone instance.
		/// </summary>
		/// <param name="server">The socket connection with the proxy server.</param>
		public AuthNone(Socket server) : base(server) {}
		/// <summary>
		/// Authenticates the user.
		/// </summary>
		public override void Authenticate() {
			return; // Do Nothing
		}
		/// <summary>
		/// Authenticates the user asynchronously.
		/// </summary>
		/// <param name="callback">The method to call when the authentication is complete.</param>
		/// <remarks>This method immediately calls the callback method.</remarks>
		public override void BeginAuthenticate(HandShakeComplete callback) {
			callback(null);
		}
	}
}
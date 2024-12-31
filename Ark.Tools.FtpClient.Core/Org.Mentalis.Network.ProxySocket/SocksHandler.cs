// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Net;
using System.Net.Sockets;

namespace Org.Mentalis.Network.ProxySocket
{
    /// <summary>
    /// References the callback method to be called when the protocol negotiation is completed.
    /// </summary>
    internal delegate void HandShakeComplete(Exception? error);
    /// <summary>
    /// Implements a specific version of the SOCKS protocol. This is an abstract class; it must be inherited.
    /// </summary>
    internal abstract class SocksHandler
    {
        /// <summary>
        /// Initializes a new instance of the SocksHandler class.
        /// </summary>
        /// <param name="server">The socket connection with the proxy server.</param>
        /// <param name="user">The username to use when authenticating with the server.</param>
        /// <exception cref="ArgumentNullException"><c>server</c> -or- <c>user</c> is null.</exception>
        protected SocksHandler(Socket server, string user)
        {
            m_Server = server;
            m_Username = user ?? string.Empty;
            m_Buffer = Array.Empty<byte>();
        }
        /// <summary>
        /// Converts a port number to an array of bytes.
        /// </summary>
        /// <param name="port">The port to convert.</param>
        /// <returns>An array of two bytes that represents the specified port.</returns>
        protected byte[] PortToBytes(int port)
        {
            byte[] ret = [(byte)(port / 256), (byte)(port % 256)];
            return ret;
        }
        /// <summary>
        /// Converts an IP address to an array of bytes.
        /// </summary>
        /// <param name="address">The IP address to convert.</param>
        /// <returns>An array of four bytes that represents the specified IP address.</returns>
        protected byte[] AddressToBytes(long address)
        {
            byte[] ret =
            [
                (byte)(address % 256),
                (byte)((address / 256) % 256),
                (byte)((address / 65536) % 256),
                (byte)(address / 16777216),
            ];
            return ret;
        }
        /// <summary>
        /// Reads a specified number of bytes from the Server socket.
        /// </summary>
        /// <param name="count">The number of bytes to return.</param>
        /// <returns>An array of bytes.</returns>
        /// <exception cref="ArgumentException">The number of bytes to read is invalid.</exception>
        /// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
        /// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
        protected byte[] ReadBytes(int count)
        {
            if (count <= 0)
                throw new ArgumentException("count should be positive", nameof(count));
            byte[] buffer = new byte[count];
            int received = 0;
            while (received != count)
            {
                int recv = Server.Receive(buffer, received, count - received, SocketFlags.None);
                if (recv == 0)
                {
                    throw new SocketException(10054);
                }
                received += recv;
            }
            return buffer;
        }
        /// <summary>
        /// Reads number of received bytes and ensures that socket was not shut down
        /// </summary>
        /// <param name="ar">IAsyncResult for receive operation</param>
        /// <returns></returns>
        protected void HandleEndReceive(IAsyncResult ar)
        {
            int recv = Server.EndReceive(ar);
            if (recv <= 0)
                throw new SocketException(10054);
            Received += recv;
        }
        /// <summary>
        /// Verifies that whole buffer was sent successfully
        /// </summary>
        /// <param name="ar">IAsyncResult for receive operation</param>
        /// <param name="expectedLength">Length of buffer that was sent</param>
        /// <returns></returns>
        protected void HandleEndSend(IAsyncResult ar, int expectedLength)
        {
            if (Server.EndSend(ar) < expectedLength)
                throw new SocketException(10054);
        }
        /// <summary>
        /// Gets or sets the socket connection with the proxy server.
        /// </summary>
        /// <value>A Socket object that represents the connection with the proxy server.</value>
        /// <exception cref="ArgumentNullException">The specified value is null.</exception>
        protected Socket Server
        {
            get
            {
                return m_Server;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                m_Server = value;
            }
        }
        /// <summary>
        /// Gets or sets the username to use when authenticating with the proxy server.
        /// </summary>
        /// <value>A string that holds the username to use when authenticating with the proxy server.</value>
        /// <exception cref="ArgumentNullException">The specified value is null.</exception>
        protected string Username
        {
            get
            {
                return m_Username;
            }
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                m_Username = value;
            }
        }
        /// <summary>
        /// Gets or sets the return value of the BeginConnect call.
        /// </summary>
        /// <value>An IAsyncProxyResult object that is the return value of the BeginConnect call.</value>
        protected IAsyncProxyResult? AsyncResult
        {
            get
            {
                return m_AsyncResult;
            }
            set
            {
                m_AsyncResult = value;
            }
        }
        /// <summary>
        /// Gets or sets a byte buffer.
        /// </summary>
        /// <value>An array of bytes.</value>
        protected byte[] Buffer
        {
            get
            {
                return m_Buffer;
            }
            set
            {
                m_Buffer = value;
            }
        }
        /// <summary>
        /// Gets or sets the number of bytes that have been received from the remote proxy server.
        /// </summary>
        /// <value>An integer that holds the number of bytes that have been received from the remote proxy server.</value>
        protected int Received
        {
            get
            {
                return m_Received;
            }
            set
            {
                m_Received = value;
            }
        }
        // private variables
        /// <summary>Holds the value of the Server property.</summary>
        private Socket m_Server;
        /// <summary>Holds the value of the Username property.</summary>
        private string m_Username;
        /// <summary>Holds the value of the AsyncResult property.</summary>
        private IAsyncProxyResult? m_AsyncResult;
        /// <summary>Holds the value of the Buffer property.</summary>
        private byte[] m_Buffer;
        /// <summary>Holds the value of the Received property.</summary>
        private int m_Received;
        /// <summary>Holds the address of the method to call when the SOCKS protocol has been completed.</summary>
        protected HandShakeComplete? ProtocolComplete;
        /// <summary>
        /// Starts negotiating with a SOCKS proxy server.
        /// </summary>
        /// <param name="host">The remote server to connect to.</param>
        /// <param name="port">The remote port to connect to.</param>
        public abstract void Negotiate(string host, int port);
        /// <summary>
        /// Starts negotiating with a SOCKS proxy server.
        /// </summary>
        /// <param name="remoteEP">The remote endpoint to connect to.</param>
        public abstract void Negotiate(IPEndPoint remoteEP);
        /// <summary>
        /// Starts negotiating asynchronously with a SOCKS proxy server.
        /// </summary>
        /// <param name="remoteEP">An IPEndPoint that represents the remote device. </param>
        /// <param name="callback">The method to call when the connection has been established.</param>
        /// <param name="proxyEndPoint">The IPEndPoint of the SOCKS proxy server.</param>
        /// <returns>An IAsyncProxyResult that references the asynchronous connection.</returns>
        public abstract IAsyncProxyResult BeginNegotiate(IPEndPoint remoteEP, HandShakeComplete callback, IPEndPoint proxyEndPoint);
        /// <summary>
        /// Starts negotiating asynchronously with a SOCKS proxy server.
        /// </summary>
        /// <param name="host">The remote server to connect to.</param>
        /// <param name="port">The remote port to connect to.</param>
        /// <param name="callback">The method to call when the connection has been established.</param>
        /// <param name="proxyEndPoint">The IPEndPoint of the SOCKS proxy server.</param>
        /// <returns>An IAsyncProxyResult that references the asynchronous connection.</returns>
        public abstract IAsyncProxyResult BeginNegotiate(string host, int port, HandShakeComplete callback, IPEndPoint proxyEndPoint);
    }
}
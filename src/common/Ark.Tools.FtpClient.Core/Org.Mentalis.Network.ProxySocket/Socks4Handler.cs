// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System.Net.Sockets;

namespace Org.Mentalis.Network.ProxySocket;

/// <summary>
/// Implements the SOCKS4[A] protocol.
/// </summary>
internal sealed class Socks4Handler : SocksHandler
{
    /// <summary>
    /// Initializes a new instance of the SocksHandler class.
    /// </summary>
    /// <param name="server">The socket connection with the proxy server.</param>
    /// <param name="user">The username to use when authenticating with the server.</param>
    /// <exception cref="ArgumentNullException"><c>server</c> -or- <c>user</c> is null.</exception>
    public Socks4Handler(Socket server, string user) : base(server, user) { }
    /// <summary>
    /// Creates an array of bytes that has to be sent when the user wants to connect to a specific host/port combination.
    /// </summary>
    /// <param name="host">The host to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <returns>An array of bytes that has to be sent when the user wants to connect to a specific host/port combination.</returns>
    /// <remarks>Resolving the host name will be done at server side. Do note that some SOCKS4 servers do not implement this functionality.</remarks>
    /// <exception cref="ArgumentNullException"><c>host</c> is null.</exception>
    /// <exception cref="ArgumentException"><c>port</c> is invalid.</exception>
    private byte[] GetHostPortBytes(string host, int port)
    {
        if (host == null)
            throw new ArgumentNullException(nameof(host));
        if (port <= 0 || port > 65535)
            throw new ArgumentException("Invalid port", nameof(port));
        byte[] connect = new byte[10 + Username.Length + host.Length];
        connect[0] = 4;
        connect[1] = 1;
        Array.Copy(PortToBytes(port), 0, connect, 2, 2);
        connect[4] = connect[5] = connect[6] = 0;
        connect[7] = 1;
        Array.Copy(Encoding.ASCII.GetBytes(Username), 0, connect, 8, Username.Length);
        connect[8 + Username.Length] = 0;
        Array.Copy(Encoding.ASCII.GetBytes(host), 0, connect, 9 + Username.Length, host.Length);
        connect[9 + Username.Length + host.Length] = 0;
        return connect;
    }
    /// <summary>
    /// Creates an array of bytes that has to be sent when the user wants to connect to a specific IPEndPoint.
    /// </summary>
    /// <param name="remoteEP">The IPEndPoint to connect to.</param>
    /// <returns>An array of bytes that has to be sent when the user wants to connect to a specific IPEndPoint.</returns>
    /// <exception cref="ArgumentNullException"><c>remoteEP</c> is null.</exception>
    private byte[] GetEndPointBytes(IPEndPoint remoteEP)
    {
        if (remoteEP == null)
            throw new ArgumentNullException(nameof(remoteEP));
        byte[] connect = new byte[9 + Username.Length];
        connect[0] = 4;
        connect[1] = 1;
        Array.Copy(PortToBytes(remoteEP.Port), 0, connect, 2, 2);
        Array.Copy(remoteEP.Address.GetAddressBytes(), 0, connect, 4, 4);
        Array.Copy(Encoding.ASCII.GetBytes(Username), 0, connect, 8, Username.Length);
        connect[8 + Username.Length] = 0;
        return connect;
    }
    /// <summary>
    /// Starts negotiating with the SOCKS server.
    /// </summary>
    /// <param name="host">The host to connect to.</param>
    /// <param name="port">The port to connect to.</param>
    /// <exception cref="ArgumentNullException"><c>host</c> is null.</exception>
    /// <exception cref="ArgumentException"><c>port</c> is invalid.</exception>
    /// <exception cref="ProxyException">The proxy rejected the request.</exception>
    /// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
    /// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
    public override void Negotiate(string host, int port)
    {
        Negotiate(GetHostPortBytes(host, port));
    }
    /// <summary>
    /// Starts negotiating with the SOCKS server.
    /// </summary>
    /// <param name="remoteEP">The IPEndPoint to connect to.</param>
    /// <exception cref="ArgumentNullException"><c>remoteEP</c> is null.</exception>
    /// <exception cref="ProxyException">The proxy rejected the request.</exception>
    /// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
    /// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
    public override void Negotiate(IPEndPoint remoteEP)
    {
        Negotiate(GetEndPointBytes(remoteEP));
    }
    /// <summary>
    /// Starts negotiating with the SOCKS server.
    /// </summary>
    /// <param name="connect">The bytes to send when trying to authenticate.</param>
    /// <exception cref="ArgumentNullException"><c>connect</c> is null.</exception>
    /// <exception cref="ArgumentException"><c>connect</c> is too small.</exception>
    /// <exception cref="ProxyException">The proxy rejected the request.</exception>
    /// <exception cref="SocketException">An operating system error occurs while accessing the Socket.</exception>
    /// <exception cref="ObjectDisposedException">The Socket has been closed.</exception>
    private void Negotiate(byte[] connect)
    {
        if (connect == null)
            throw new ArgumentNullException(nameof(connect));
        if (connect.Length < 2)
            throw new ArgumentException("Expected at least 2 bytes, got less", nameof(connect));
        if (Server.Send(connect) < connect.Length)
            throw new SocketException(10054);
        byte[] buffer = ReadBytes(8);
        if (buffer[1] != 90)
        {
            Server.Close();
            throw new ProxyException("Negotiation failed.");
        }
    }
    /// <summary>
    /// Starts negotiating asynchronously with a SOCKS proxy server.
    /// </summary>
    /// <param name="host">The remote server to connect to.</param>
    /// <param name="port">The remote port to connect to.</param>
    /// <param name="callback">The method to call when the connection has been established.</param>
    /// <param name="proxyEndPoint">The IPEndPoint of the SOCKS proxy server.</param>
    /// <returns>An IAsyncProxyResult that references the asynchronous connection.</returns>
    public override IAsyncProxyResult BeginNegotiate(string host, int port, HandShakeComplete callback, IPEndPoint proxyEndPoint)
    {
        ProtocolComplete = callback;
        Buffer = GetHostPortBytes(host, port);
        Server.BeginConnect(proxyEndPoint, new AsyncCallback(this.OnConnect), Server);
        AsyncResult = new IAsyncProxyResult();
        return AsyncResult;
    }
    /// <summary>
    /// Starts negotiating asynchronously with a SOCKS proxy server.
    /// </summary>
    /// <param name="remoteEP">An IPEndPoint that represents the remote device.</param>
    /// <param name="callback">The method to call when the connection has been established.</param>
    /// <param name="proxyEndPoint">The IPEndPoint of the SOCKS proxy server.</param>
    /// <returns>An IAsyncProxyResult that references the asynchronous connection.</returns>
    public override IAsyncProxyResult BeginNegotiate(IPEndPoint remoteEP, HandShakeComplete callback, IPEndPoint proxyEndPoint)
    {
        ProtocolComplete = callback;
        Buffer = GetEndPointBytes(remoteEP);
        Server.BeginConnect(proxyEndPoint, new AsyncCallback(this.OnConnect), Server);
        AsyncResult = new IAsyncProxyResult();
        return AsyncResult;
    }
    /// <summary>
    /// Called when the Socket is connected to the remote proxy server.
    /// </summary>
    /// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
    private void OnConnect(IAsyncResult ar)
    {
        try
        {
            Server.EndConnect(ar);
        }
        catch (Exception e)
        {
            ProtocolComplete?.Invoke(e);
            return;
        }
        try
        {
            Server.BeginSend(Buffer, 0, Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnSent), Server);
        }
        catch (Exception e)
        {
            ProtocolComplete?.Invoke(e);
        }
    }
    /// <summary>
    /// Called when the Socket has sent the handshake data.
    /// </summary>
    /// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
    private void OnSent(IAsyncResult ar)
    {
        try
        {
            HandleEndSend(ar, Buffer.Length);
        }
        catch (Exception e)
        {
            ProtocolComplete?.Invoke(e);
            return;
        }
        try
        {
            Buffer = new byte[8];
            Received = 0;
            Server.BeginReceive(Buffer, 0, Buffer.Length, SocketFlags.None, new AsyncCallback(this.OnReceive), Server);
        }
        catch (Exception e)
        {
            ProtocolComplete?.Invoke(e);
        }
    }
    /// <summary>
    /// Called when the Socket has received a reply from the remote proxy server.
    /// </summary>
    /// <param name="ar">Stores state information for this asynchronous operation as well as any user-defined data.</param>
    private void OnReceive(IAsyncResult ar)
    {
        try
        {
            HandleEndReceive(ar);
            if (Received == 8)
            {
                if (Buffer[1] == 90)
                    ProtocolComplete?.Invoke(null);
                else
                {
                    Server.Close();
                    ProtocolComplete?.Invoke(new ProxyException("Negotiation failed."));
                }
            }
            else
            {
                Server.BeginReceive(Buffer, Received, Buffer.Length - Received, SocketFlags.None, new AsyncCallback(this.OnReceive), Server);
            }
        }
        catch (Exception e)
        {
            ProtocolComplete?.Invoke(e);
        }
    }
}
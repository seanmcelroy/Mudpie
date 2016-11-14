// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Listener.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A listener is a network server process that listens for and accepts incoming <see cref="Network.Connection" />s
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Network
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;

    using JetBrains.Annotations;

    using log4net;

    /// <summary>
    /// A listener is a network server process that listens for and accepts incoming <see cref="Network.Connection"/>s
    /// </summary>
    internal class Listener : TcpListener
    {
        /// <summary>
        /// The logging instance for this class
        /// </summary>
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(Listener));

        /// <summary>
        /// Thread signal
        /// </summary>
        private readonly Server _server;

        /// <summary>
        /// Initializes a new instance of the <see cref="Listener"/> class.
        /// </summary>
        /// <param name="server">The server instance that will own the accepted connection</param>
        /// <param name="localEp">The local endpoint connection information</param>
        public Listener([NotNull] Server server, [NotNull] IPEndPoint localEp)
            : base(localEp)
        {
            this._server = server;
        }

        public PortClass PortType { get; set; }

        public async void StartAccepting()
        {
            // Establish the local endpoint for the socket.
            var localEndPoint = new IPEndPoint(IPAddress.Any, ((IPEndPoint)this.LocalEndpoint).Port);

            // Create a TCP/IP socket.
            var listener = new Listener(this._server, localEndPoint);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Start(100);

                while (true)
                {
                    // Start an asynchronous socket to listen for connections.
                    var handler = await listener.AcceptTcpClientAsync();
                    if (handler == null)
                        continue;

                    // Create the state object.
                    Connection connection;

                    if (this.PortType == PortClass.ClearText)
                    {
                        var stream = handler.GetStream();

                        connection = new Connection(this._server, handler, stream);
                    }
                    else
                    {
                        var stream = handler.GetStream();
                        var sslStream = new SslStream(stream);

                        try
                        {
                            await sslStream.AuthenticateAsServerAsync(this._server.ServerAuthenticationCertificate);
                        }
                        catch (IOException ioe)
                        {
                            _Logger.Error("I/O Exception attempting to perform TLS handshake", ioe);
                            return;
                        }

                        connection = new Connection(this._server, handler, sslStream, true);
                    }

                    this._server.AddConnection(connection);

                    connection.Process();
                }

            }
            catch (Exception ex)
            {
                _Logger.Error("Exception when trying to accept connection from listener", ex);
            }
        }
    }
}

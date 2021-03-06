﻿// --------------------------------------------------------------------------------------------------------------------
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
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

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
        [NotNull]
        // ReSharper disable once AssignNullToNotNullAttribute
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Listener));

        /// <summary>
        /// Thread signal
        /// </summary>
        [NotNull]
        private readonly Server server;

        /// <summary>
        /// Initializes a new instance of the <see cref="Listener"/> class.
        /// </summary>
        /// <param name="server">The server instance that will own the accepted connection</param>
        /// <param name="localEp">The local endpoint connection information</param>
        public Listener([NotNull] Server server, [NotNull] IPEndPoint localEp)
            : base(localEp)
        {
            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            if (localEp == null)
            {
                throw new ArgumentNullException(nameof(localEp));
            }

            this.server = server;
        }

        /// <summary>
        /// Gets or sets the type of port (encrypted or plain-text) of a connection
        /// </summary>
        public PortClass PortType { get; set; }

        /// <summary>
        /// Begins accept connections on the connection listener
        /// </summary>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>A task object used to await this method for completion</returns>
        [NotNull]
        public async Task StartAcceptingAsync(CancellationToken cancellationToken)
        {
            if (this.LocalEndpoint == null)
            {
                throw new InvalidOperationException("The local endpoint for this listener is null");
            }

            // Establish the local endpoint for the socket.
            var localEndPoint = new IPEndPoint(IPAddress.Any, ((IPEndPoint)this.LocalEndpoint).Port);

            // Create a TCP/IP socket.
            var listener = new Listener(this.server, localEndPoint);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Start(100);

                while (true)
                {
                    // Start an asynchronous socket to listen for connections.
                    var handler = await listener.AcceptTcpClientAsync();
                    if (handler == null)
                    {
                        continue;
                    }

                    // Create the state object.
                    var stream = handler.GetStream();
                    var connection = new Connection(this.server, handler, stream);
                    this.server.AddConnection(connection);

                    connection.Process(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Exception when trying to accept connection from listener", ex);
            }
        }
    }
}

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
            this.server = server;
        }

        public PortClass PortType { get; set; }

        [NotNull]
        public async Task StartAcceptingAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
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

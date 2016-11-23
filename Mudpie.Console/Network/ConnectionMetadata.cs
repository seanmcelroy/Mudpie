// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConnectionMetadata.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   Metadata about a connection from a client to the server instance
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Mudpie.Console.Network
{
    using System;
    using System.Net;

    using JetBrains.Annotations;

    /// <summary>
    /// Metadata about a connection from a client to the server instance
    /// </summary>
    [PublicAPI]
    internal class ConnectionMetadata
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionMetadata"/> class.
        /// </summary>
        /// <param name="connection">The connection this metadata is associated with</param>
        public ConnectionMetadata([NotNull] Connection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            this.Connection = connection;
        }

        /// <summary>
        /// Gets or sets the remote address of the client that is connected to the server
        /// </summary>
        [NotNull]
        public IPAddress RemoteAddress => this.Connection.RemoteAddress;

        /// <summary>
        /// Gets or sets the remote port of the client that is connected to the server
        /// </summary>
        public int RemotePort => this.Connection.RemotePort;

        /// <summary>
        /// Gets or sets the number of messages sent over this connection
        /// </summary>
        public ulong SentMessageCount { get; set; }

        /// <summary>
        /// Gets or sets the amount of data sent over this connection in bytes
        /// </summary>
        public ulong SentMessageBytes { get; set; }

        /// <summary>
        /// Gets or sets the number of messages received over this connection
        /// </summary>
        public ulong RecvMessageCount { get; set; }

        /// <summary>
        /// Gets or sets the amount of data received over this connection in bytes
        /// </summary>
        public ulong RecvMessageBytes { get; set; }

        /// <summary>
        /// Gets or sets the date the connection was opened, stored as a UTC value
        /// </summary>
        public DateTime Established { get; set; }

        /// <summary>
        /// Gets or sets the address that was listening for this connection when it was received, if this connection was an inbound address
        /// </summary>
        [CanBeNull]
        public IPAddress ListenAddress { get; set; }

        /// <summary>
        /// Gets or sets the port that was listening for this connection when it was received, if this connection was an inbound address
        /// </summary>
        [CanBeNull]
        public int? ListenPort { get; set; }

        /// <summary>
        /// Gets or sets the username as authenticated successfully by the client to the server, if authenticated
        /// </summary>
        [CanBeNull]
        public string AuthenticatedUsername => this.Connection.Identity?.Username;

        /// <summary>
        /// Gets or sets the name of the principal associated with this connection
        /// </summary>
        /// <remarks>
        /// The user may have a principal but not be authenticated, such as with an anonymous user
        /// </remarks>
        [CanBeNull]
        public string PrincipalName => this.Connection.Identity?.Username;

        /// <summary>
        /// Gets the connection this metadata is associated with
        /// </summary>
        [NotNull]
        public Connection Connection { get; private set; }
    }
}

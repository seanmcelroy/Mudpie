// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Server.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   The server is the holder of <see cref="Listener" /> and <see cref="Connection" /> objects used to manage network communications
//   with players
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Network
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Security;
    using System.Security.Permissions;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Mudpie.Server.Data;

    /// <summary>
    /// The server is the holder of <see cref="Listener"/> and <see cref="Connection"/> objects used to manage network communications
    /// with players
    /// </summary>
    internal class Server
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        [NotNull]
        // ReSharper disable once AssignNullToNotNullAttribute
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Server));

        /// <summary>
        /// A list of threads and the associated TCP new-connection listeners that are serviced by each by the client
        /// </summary>
        [NotNull]
        private readonly List<Tuple<Thread, Listener>> listeners = new List<Tuple<Thread, Listener>>();

        /// <summary>
        /// A list of connections currently established to this server instance
        /// </summary>
        [NotNull]
        private readonly List<Connection> connections = new List<Connection>();

        /// <summary>
        /// A cancellation source that is used to cancel all asynchronous operations within the server once the <see cref="Stop"/> operation is called
        /// </summary>
        [NotNull]
        private CancellationTokenSource cts = new CancellationTokenSource();

        /// <summary>
        /// Initializes a new instance of the <see cref="Server"/> class.
        /// </summary>
        /// <param name="clearPorts">
        /// The ports over which clear-text communications are permitted
        /// </param>
        /// <param name="scriptingEngine">
        /// The scripting engine used to execute programs invoked or triggered by the user
        /// </param>
        public Server([NotNull] int[] clearPorts, [NotNull] Scripting.Engine scriptingEngine)
        {
            if (clearPorts == null)
            {
                throw new ArgumentNullException(nameof(clearPorts));
            }

            if (scriptingEngine == null)
            {
                throw new ArgumentNullException(nameof(scriptingEngine));
            }

            this.ShowData = true;
            this.ClearPorts = clearPorts;
            this.ScriptingEngine = scriptingEngine;
        }

        /// <summary>
        /// Gets the metadata about the connections to this server instance
        /// </summary>
        [NotNull]
        public IReadOnlyList<ConnectionMetadata> Connections => this.connections.Where(c => c != null).Select(c => new ConnectionMetadata(c)).ToList().AsReadOnly();

        /// <summary>
        /// Gets or sets a value indicating whether the byte transmitted counts are logged to the logging instance
        /// </summary>
        [PublicAPI]
        public bool ShowBytes { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the commands transmitted are logged to the logging instance
        /// </summary>
        [PublicAPI]
        public bool ShowCommands { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the actual bytes (data) transmitted are logged to the logging instance
        /// </summary>
        [PublicAPI]
        public bool ShowData { get; set; }

        /// <summary>
        /// Gets the cancellation token traced by this server
        /// </summary>
        public CancellationToken CancellationToken => this.cts.Token;

        /// <summary>
        /// Gets the scripting engine that will handle commands sent by connections to the server
        /// </summary>
        [NotNull]
        internal Scripting.Engine ScriptingEngine { get; }

        /// <summary>
        /// Gets the ports over which clear-text communications are permitted
        /// </summary>
        [NotNull]
        private int[] ClearPorts { get; }

        #region Connection and IO
        /// <summary>
        /// Starts listener threads to begin processing requests
        /// </summary>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Thrown when an error occurs while a SSL certificate is loaded to support TLS-enabled ports</exception>
        /// <exception cref="SecurityException">Thrown when the certificate store cannot be successfully opened to look up a SSL certificate by its thumbprint</exception>
        [StorePermission(SecurityAction.Demand, EnumerateCertificates = true, OpenStore = true)]
        public void Start()
        {
            // PRECOMPILE MAJOR PROGRAMS
            var precompileTask = Task.Run(
                                     async () =>
                                         {
                                             var voidRoom =
                                                 await
                                                     CacheManager.LookupOrRetrieveAsync(
                                                         1,
                                                         this.ScriptingEngine.Redis,
                                                         (d, token) =>
                                                                 Room.GetAsync(this.ScriptingEngine.Redis, d, token),
                                                         default(CancellationToken));
                                             Debug.Assert(voidRoom != null, "voidRoom != null");
                                             if (voidRoom.Contents != null)
                                             {
                                                 foreach (var linkComposed in voidRoom.Contents.Where(c => c.DataObject.GetType() == typeof(Link)))
                                                 {
                                                     var target = await ObjectBase.GetAsync(this.ScriptingEngine.Redis, ((Link)linkComposed.DataObject).Target, this.CancellationToken);
                                                     (target as Program)?.Compile();
                                                 }
                                             }
                                         }, 
                                     this.CancellationToken);
            if (!precompileTask.Wait(60000))
            {
                Logger.Warn("Unable to finish pre-compilation of programs in the Void(#000001) room in 60 seconds");
                return;
            }

            // NETWORKING
            this.listeners.Clear();
            
            foreach (var clearPort in this.ClearPorts)
            {
                // Establish the local endpoint for the socket.
                var localEndPoint = new IPEndPoint(IPAddress.Any, clearPort);

                // Create a TCP/IP socket.
                var listener = new Listener(this, localEndPoint)
                {
                    PortType = PortClass.ClearText
                };

                this.listeners.Add(new Tuple<Thread, Listener>(new Thread(async () => await listener.StartAcceptingAsync(this.cts.Token)), listener));
            }

            foreach (var listener in this.listeners)
            {
                try
                {
                    if (listener.Item1 != null)
                    {
                        listener.Item1.Start();
                        Logger.InfoFormat("Listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
                    }
                }
                catch (OutOfMemoryException oom)
                {
                    Logger.Error("Unable to start listener thread.  Not enough memory.", oom);
                    this.cts.Cancel();
                }
            }
        }

        public void Stop()
        {
            this.cts.Cancel();

            foreach (var listener in this.listeners)
            {
                try
                {
                    listener.Item2.Stop();
                    Logger.InfoFormat("Stopped listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
                }
                catch (SocketException)
                {
                    Logger.ErrorFormat("Exception attempting to stop listening on port {0} ({1})", ((IPEndPoint)listener.Item2.LocalEndpoint).Port, listener.Item2.PortType);
                }
            }

            Task.WaitAll(this.connections.Select(connection => connection?.ShutdownAsync()).ToArray());

            foreach (var thread in this.listeners)
            {
                try
                {
                    thread.Item1?.Abort();
                }
                catch (SecurityException se)
                {
                    Logger.Error(
                        "Unable to abort the thread due to a security exception.  Application will now exit.",
                        se);
                    Environment.Exit(se.HResult);
                }
                catch (ThreadStateException tse)
                {
                    Logger.Error(
                        "Unable to abort the thread due to a thread state exception.  Application will now exit.",
                        tse);
                    Environment.Exit(tse.HResult);
                }
            }

            // Reset for next run
            this.cts = new CancellationTokenSource();
        }

        internal void AddConnection([NotNull] Connection connection)
        {
            this.connections.Add(connection);
            Logger.VerboseFormat(
                "Connection from {0}:{1} to {2}:{3}",
                connection.RemoteAddress,
                connection.RemotePort,
                connection.LocalAddress,
                connection.LocalPort);
        }

        internal void RemoveConnection([NotNull] Connection connection)
        {
            this.connections.Remove(connection);
            if (connection.Identity == null)
            {
                Logger.VerboseFormat(
                    "Disconnection from {0}:{1}",
                    connection.RemoteAddress,
                    connection.RemotePort,
                    connection.LocalAddress,
                    connection.LocalPort);
            }
            else
            {
                Logger.VerboseFormat(
                    "Disconnection from {0}:{1} ({2})",
                    connection.RemoteAddress,
                    connection.RemotePort,
                    connection.LocalAddress,
                    connection.LocalPort,
                    connection.Identity.Username);
            }
        }
        #endregion
    }
}

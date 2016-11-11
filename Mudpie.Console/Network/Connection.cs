using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mudpie.Console.Network
{
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;

    using JetBrains.Annotations;

    using log4net;

    using Data;
    using System.Threading;

    internal class Connection
    {
        /// <summary>
        /// The size of the stream receive buffer
        /// </summary>
        private const int BufferSize = 1024;

        /// <summary>
        /// A command-indexed dictionary with function pointers to support client command
        /// </summary>
        private static readonly Dictionary<string, Func<Connection, string, Task<CommandProcessingResult>>> BuiltInCommandDirectory;

        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Connection));

        /// <summary>
        /// The server instance to which this connection belongs
        /// </summary>
        [NotNull]
        private readonly Server server;

        /// <summary>
        /// The <see cref="TcpClient"/> that accepted this connection.
        /// </summary>
        [NotNull]
        private readonly TcpClient client;

        /// <summary>
        /// The <see cref="Stream"/> instance retrieved from the <see cref="TcpClient"/> that accepted this connection.
        /// </summary>
        [NotNull]
        private readonly Stream stream;

        /// <summary>
        /// The stream receive buffer
        /// </summary>
        [NotNull]
        private readonly byte[] buffer = new byte[BufferSize];

        /// <summary>
        /// The received data buffer appended to from the stream buffer
        /// </summary>
        [NotNull]
        private readonly StringBuilder builder = new StringBuilder();

        /// <summary>
        /// The remote IP address to which the connection is established
        /// </summary>
        [NotNull]
        private readonly IPAddress remoteAddress;

        /// <summary>
        /// The remote TCP port number for the remote endpoint to which the connection is established
        /// </summary>
        private readonly int remotePort;

        /// <summary>
        /// The local IP address to which the connection is established
        /// </summary>
        [NotNull]
        private readonly IPAddress localAddress;

        /// <summary>
        /// The local TCP port number for the local endpoint to which the connection is established
        /// </summary>
        private readonly int localPort;

        /// <summary>
        /// For commands that handle conversational request-replies, this is a reference to the
        /// command that should handle new input received by the main process loop.
        /// </summary>
        [CanBeNull]
        private CommandProcessingResult inProcessCommand;

        /// <summary>
        /// Gets or sets the mode for the connection.  When a connection is in <see cref="ConnectionMode.Normal"/> mode,
        /// commands will be process by the global command resolution system.  When the connection is in <see cref="ConnectionMode.InteractiveProgram"/>,
        /// all input is directed to a running program that is serving the connection.
        /// </summary>
        public ConnectionMode Mode { get; private set; } = ConnectionMode.Normal;

        /// <summary>
        /// The handler that receives messages while the <see cref="Mode"/> is set to <see cref="ConnectionMode.InteractiveProgram"/>
        /// </summary>
        [CanBeNull]
        private Action<string> programInputHandler;

        /// <summary>
        /// Initializes static members of the <see cref="Connection"/> class.
        /// </summary>
        static Connection()
        {
            BuiltInCommandDirectory = new Dictionary<string, Func<Connection, string, Task<CommandProcessingResult>>>
                {
                    { "CAPABILITIES", async (c, data) => await c.Capabilities() }
                };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Connection"/> class.
        /// </summary>
        /// <param name="server">The server instance that owns this connection</param>
        /// <param name="client">The <see cref="TcpClient"/> that accepted this connection</param>
        /// <param name="stream">The <see cref="Stream"/> from the <paramref name="client"/></param>
        /// <param name="tls">Whether or not the connection has implicit Transport Layer Security</param>
        public Connection(
            [NotNull] Server server,
            [NotNull] TcpClient client,
            [NotNull] Stream stream,
            bool tls = false)
        {
            this.client = client;
            this.client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            this.ShowBytes = server.ShowBytes;
            this.ShowCommands = server.ShowCommands;
            this.ShowData = server.ShowData;
            this.server = server;
            this.stream = stream;

            var remoteIpEndpoint = (IPEndPoint)this.client.Client.RemoteEndPoint;
            this.remoteAddress = remoteIpEndpoint.Address;
            this.remotePort = remoteIpEndpoint.Port;
            var localIpEndpoint = (IPEndPoint)this.client.Client.LocalEndPoint;
            this.localAddress = localIpEndpoint.Address;
            this.localPort = localIpEndpoint.Port;
        }

        public bool ShowBytes { get; set; }

        public bool ShowCommands { get; set; }

        public bool ShowData { get; set; }

        #region Authentication
        [CanBeNull]
        public string Username { get; set; }

        [CanBeNull]
        public Player Identity { get; set; }
        #endregion

        /// <summary>
        /// Gets the newsgroup currently selected by this connection
        /// </summary>
        [PublicAPI, CanBeNull]
        public string CurrentRoomId { get; private set; }

        #region Derived instance properties
        /// <summary>
        /// Gets the remote IP address to which the connection is established
        /// </summary>
        [NotNull]
        public IPAddress RemoteAddress => this.remoteAddress;

        /// <summary>
        /// Gets the remote TCP port number for the remote endpoint to which the connection is established
        /// </summary>
        public int RemotePort
        {
            get { return this.remotePort; }
        }

        /// <summary>
        /// Gets the local IP address to which the connection is established
        /// </summary>
        [NotNull]
        public IPAddress LocalAddress
        {
            get { return this.localAddress; }
        }

        /// <summary>
        /// Gets the local TCP port number for the local endpoint to which the connection is established
        /// </summary>
        public int LocalPort
        {
            get { return this.localPort; }
        }
        #endregion

        #region IO and Connection Management
        public async void Process(CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.SendAsync("200 Service available, posting allowed\r\n");

            Debug.Assert(this.stream != null, "The stream was 'null', but it should not have been because the connection was accepted and processing is beginning.");

            bool send403;

            try
            {
                while (true)
                {
                    if (!this.client.Connected || !this.client.Client.Connected) return;

                    if (!this.stream.CanRead)
                    {
                        await this.Shutdown();
                        return;
                    }

                    var bytesRead = await this.stream.ReadAsync(this.buffer, 0, BufferSize);

                    // There  might be more data, so store the data received so far.
                    this.builder.Append(Encoding.ASCII.GetString(this.buffer, 0, bytesRead));

                    // Not all data received OR no more but not yet ending with the delimiter. Get more.
                    var content = this.builder.ToString();
                    if (bytesRead == BufferSize || !content.EndsWith("\r\n", StringComparison.Ordinal))
                    {
                        // Read some more.
                        continue;
                    }

                    // All the data has been read from the 
                    // client. Display it on the console.
                    if (this.ShowBytes && this.ShowData)
                        Logger.TraceFormat(
                            "{0}:{1} >{2}> {3} bytes: {4}", this.RemoteAddress, this.RemotePort, ">",
                            content.Length,
                            content.TrimEnd('\r', '\n'));
                    else if (this.ShowBytes)
                        Logger.TraceFormat(
                            "{0}:{1} >{2}> {3} bytes", this.RemoteAddress, this.RemotePort, ">",
                            content.Length);
                    else if (this.ShowData)
                        Logger.TraceFormat(
                            "{0}:{1} >{2}> {3}", this.RemoteAddress, this.RemotePort, ">",
                            content.TrimEnd('\r', '\n'));

                    if (this.inProcessCommand != null && this.inProcessCommand.MessageHandler != null)
                    {
                        // Ongoing read - don't parse it for commands
                        this.inProcessCommand = await this.inProcessCommand.MessageHandler(content, this.inProcessCommand);
                        if (this.inProcessCommand != null && this.inProcessCommand.IsQuitting)
                            this.inProcessCommand = null;
                    }
                    else if (this.Mode == ConnectionMode.InteractiveProgram)
                    {
                        // Send input instead to the program bound to this connection
                        Debug.Assert(programInputHandler != null);
                        programInputHandler.Invoke(content);
                    }
                    else
                    {
                        var command = content.Split(' ').First().TrimEnd('\r', '\n').ToUpperInvariant();
                        if (BuiltInCommandDirectory.ContainsKey(command))
                        {
                            try
                            {
                                if (this.ShowCommands)
                                    Logger.TraceFormat("{0}:{1} >{2}> {3}", this.RemoteAddress, this.RemotePort, ">", content.TrimEnd('\r', '\n'));

                                var result = await BuiltInCommandDirectory[command].Invoke(this, content);

                                if (!result.IsHandled) await this.SendAsync("500 Unknown command\r\n");
                                else if (result.MessageHandler != null) this.inProcessCommand = result;
                                else if (result.IsQuitting) return;
                            }
                            catch (Exception ex)
                            {
                                send403 = true;
                                Logger.Error("Exception processing a command", ex);
                                break;
                            }
                        }
                        else
                        {
                            var context = await server.ScriptingEngine.RunProgramAsync<int>(command, this.Identity, this, cancellationToken);
                            if (context == null || context.ErrorNumber == Scripting.ContextErrorNumber.ProgramNotFound || context.ErrorNumber == Scripting.ContextErrorNumber.ProgramNotSpecified)
                                await this.SendAsync("Huh?\r\n");
                            else if (context.ErrorNumber == Scripting.ContextErrorNumber.AuthenticationRequired)
                                await this.SendAsync("You must be logged in to use that command.\r\n");
                            else if (context.State == Scripting.ContextState.Aborted)
                                await this.SendAsync("Aborted.\r\n");
                            else if (context.State == Scripting.ContextState.Errored)
                                await this.SendAsync($"ERROR: {context.ErrorMessage}\r\n");
                            else if (context.State == Scripting.ContextState.Killed)
                                await this.SendAsync($"KILLED: {context.ErrorMessage}\r\n");
                            else if (context.State == Scripting.ContextState.Loaded)
                                await this.SendAsync($"STUCK: {context.ProgramName} loaded but not completed.\r\n");
                            else if (context.State == Scripting.ContextState.Paused)
                                await this.SendAsync($"Paused: {context.ProgramName}.\r\n");
                            else if (context.State == Scripting.ContextState.Running)
                                await this.SendAsync($"Running... {context.ProgramName}.\r\n");
                            else if (context.State == Scripting.ContextState.Completed)
                            {
                                // Write feedback to output
                                if (context.Output.Count == 0)
                                    await this.SendAsync($"{context.ProgramName} complete.  Result:{context.ReturnValue}\r\n");
                                else
                                    foreach (var line in context.Output)
                                        await this.SendAsync($"{line}\r\n");
                            }
                        }
                    }

                    this.builder.Clear();
                }
            }
            catch (DecoderFallbackException dfe)
            {
                send403 = true;
                Logger.Error("Decoder Fallback Exception socket " + this.RemoteAddress, dfe);
            }
            catch (IOException se)
            {
                send403 = true;
                Logger.Error("I/O Exception on socket " + this.RemoteAddress, se);
            }
            catch (SocketException se)
            {
                send403 = true;
                Logger.Error("Socket Exception on socket " + this.RemoteAddress, se);
            }
            catch (NotSupportedException nse)
            {
                Logger.Error("Not Supported Exception", nse);
                return;
            }
            catch (ObjectDisposedException ode)
            {
                Logger.Error("Object Disposed Exception", ode);
                return;
            }

            if (send403)
                await this.SendAsync("403 Archive server temporarily offline\r\n");
        }

        /// <summary>
        /// Sends the formatted data to the client
        /// </summary>
        /// <param name="format">The data, or format string for data, to send to the client</param>
        /// <param name="args">The argument applied as a format string to <paramref name="format"/> to create the data to send to the client</param>
        /// <returns>A value indicating whether or not the transmission was successful</returns>
        [StringFormatMethod("format"), NotNull]
        internal async Task<bool> SendAsync([NotNull] string format, [NotNull] params object[] args)
        {
            return await this.SendInternalAsync(string.Format(CultureInfo.InvariantCulture, format, args));
        }

        private async Task<bool> SendInternalAsync([NotNull] string data)
        {
            // Convert the string data to byte data using ASCII encoding.
            var byteData = Encoding.UTF8.GetBytes(data);

            try
            {
                // Begin sending the data to the remote device.
                await this.stream.WriteAsync(byteData, 0, byteData.Length);
                if (this.ShowBytes && this.ShowData)
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes: {5}",
                        this.RemoteAddress, 
                        this.RemotePort,
                        "<",
                        "<",
                        byteData.Length,
                        data.TrimEnd('\r', '\n'));
                else if (this.ShowBytes)
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes",
                        this.RemoteAddress, 
                        this.RemotePort, 
                        "<",
                        "<",
                        byteData.Length);
                else if (this.ShowData)
                    Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4}",
                        this.RemoteAddress, 
                        this.RemotePort,
                        "<",
                        "<",
                        data.TrimEnd('\r', '\n'));

                return true;
            }
            catch (IOException)
            {
                // Don't send 403 - the sending socket isn't working.
                Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
            catch (SocketException)
            {
                // Don't send 403 - the sending socket isn't working.
                Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
            catch (ObjectDisposedException)
            {
                Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
        }

        public void RedirectInputToProgram([NotNull] Action<string> programInputHandler)
        {
            this.programInputHandler = programInputHandler;
            Mode = ConnectionMode.InteractiveProgram;
        }

        public void ResetInputRedirection()
        {
            Mode = ConnectionMode.Normal;
            this.programInputHandler = null;
        }

        public async Task Shutdown()
        {
            if (this.client.Connected)
            {
                await this.SendAsync("205 closing connection\r\n");
                this.client.Client.Shutdown(SocketShutdown.Both);
                this.client.Close();
            }

            this.server.RemoveConnection(this);
        }
        #endregion

        #region Commands
        /// <summary>
        /// Handles the CAPABILITIES command from a client, which allows a client to retrieve a list
        /// of the functionality available in this server. 
        /// </summary>
        /// <returns>A command processing result specifying the command is handled.</returns>
        /// <remarks>See <a href="http://tools.ietf.org/html/rfc3977#section-5.2">RFC 3977</a> for more information.</remarks>
        private async Task<CommandProcessingResult> Capabilities()
        {
            var sb = new StringBuilder();
            sb.Append("101 Capability list:\r\n");
            sb.Append("VERSION 2\r\n");

            // sb.Append("IHAVE\r\n");
            sb.Append("HDR\r\n");
            sb.Append("LIST ACTIVE NEWSGROUPS ACTIVE.TIMES DISTRIB.PATS HEADERS OVERVIEW.FMT\r\n");
            sb.Append("MODE-READER\r\n");
            sb.Append("NEWNEWS\r\n");
            sb.Append("OVER MSGID\r\n");
            sb.Append("POST\r\n");
            sb.Append("READER\r\n");
            sb.Append("XFEATURE-COMPRESS GZIP TERMINATOR\r\n");
            sb.Append("IMPLEMENTATION McNNTP 1.0.0\r\n");
            sb.Append(".\r\n");
            await this.SendAsync(sb.ToString());
            return new CommandProcessingResult(true);
        }
        #endregion

        [CanBeNull, Pure]
        private static Tuple<int, int?> ParseRange([NotNull] string input)
        {
            int low, high;
            if (input.IndexOf('-') == -1)
            {
                return !int.TryParse(input, out low)
                    ? default(Tuple<int, int?>)
                    : new Tuple<int, int?>(low, low);
            }

            if (input.EndsWith("-", StringComparison.Ordinal))
            {
                return !int.TryParse(input, out low)
                    ? default(Tuple<int, int?>)
                    : new Tuple<int, int?>(low, null);
            }

            if (!int.TryParse(input.Substring(0, input.IndexOf('-')), NumberStyles.Integer, CultureInfo.InvariantCulture, out low))
                return default(Tuple<int, int?>);
            if (!int.TryParse(input.Substring(input.IndexOf('-') + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out high))
                return default(Tuple<int, int?>);

            return new Tuple<int, int?>(low, high);
        }
    }
}

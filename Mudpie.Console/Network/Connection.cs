// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Connection.cs" company="Sean McElroy">
//   Released under the terms of the MIT License//   
// </copyright>
// <summary>
//   A persistent, accepted connection from a client computer to the network server process
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Network
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Security;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Data;

    using JetBrains.Annotations;

    using log4net;

    using Mudpie.Scripting.Common;

    /// <summary>
    /// A persistent, accepted connection from a client computer to the network server process
    /// </summary>
    internal class Connection
    {
        /// <summary>
        /// The size of the stream receive buffer
        /// </summary>
        private const int BufferSize = 1024;

        /// <summary>
        /// A command-indexed dictionary with function pointers to support client command
        /// </summary>
        private static readonly Dictionary<string, Func<Connection, string, Task<CommandProcessingResult>>> _BuiltInCommandDirectory;

        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(Connection));

        /// <summary>
        /// The server instance to which this connection belongs
        /// </summary>
        [NotNull]
        private readonly Server _server;

        /// <summary>
        /// The <see cref="TcpClient"/> that accepted this connection.
        /// </summary>
        [NotNull]
        private readonly TcpClient _client;

        /// <summary>
        /// The <see cref="Stream"/> instance retrieved from the <see cref="TcpClient"/> that accepted this connection.
        /// </summary>
        [NotNull]
        private readonly Stream _stream;

        /// <summary>
        /// The stream receive buffer
        /// </summary>
        [NotNull]
        private readonly byte[] _buffer = new byte[BufferSize];

        /// <summary>
        /// The received data buffer appended to from the stream buffer
        /// </summary>
        [NotNull]
        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>
        /// For commands that handle conversational request-replies, this is a reference to the
        /// command that should handle new input received by the main process loop.
        /// </summary>
        [CanBeNull]
        private CommandProcessingResult _inProcessCommand;

        /// <summary>
        /// The handler that receives messages while the <see cref="Mode"/> is set to <see cref="ConnectionMode.InteractiveProgram"/>
        /// </summary>
        [CanBeNull]
        private Action<string> _programInputHandler;

        /// <summary>
        /// Initializes static members of the <see cref="Connection"/> class.
        /// </summary>
        static Connection()
        {
            _BuiltInCommandDirectory = new Dictionary<string, Func<Connection, string, Task<CommandProcessingResult>>>
                {
                    { "CONNECT", async (c, data) => await c.ConnectAsync(data) }
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
            this._client = client;
            this._client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            this.ShowBytes = server.ShowBytes;
            this.ShowCommands = server.ShowCommands;
            this.ShowData = server.ShowData;
            this._server = server;
            this._stream = stream;

            var remoteIpEndpoint = (IPEndPoint)this._client.Client.RemoteEndPoint;
            Debug.Assert(remoteIpEndpoint != null, "remoteIpEndpoint != null");
            Debug.Assert(remoteIpEndpoint.Address != null, "remoteIpEndpoint.Address != null");
            this.RemoteAddress = remoteIpEndpoint.Address;
            this.RemotePort = remoteIpEndpoint.Port;
            var localIpEndpoint = (IPEndPoint)this._client.Client.LocalEndPoint;
            Debug.Assert(localIpEndpoint != null, "localIpEndpoint != null");
            Debug.Assert(localIpEndpoint.Address != null, "localIpEndpoint.Address != null");
            this.LocalAddress = localIpEndpoint.Address;
            this.LocalPort = localIpEndpoint.Port;
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
        public IPAddress RemoteAddress { get; }

        /// <summary>
        /// Gets the remote TCP port number for the remote endpoint to which the connection is established
        /// </summary>
        public int RemotePort { get; }

        /// <summary>
        /// Gets the local IP address to which the connection is established
        /// </summary>
        [NotNull]
        public IPAddress LocalAddress { get; }

        /// <summary>
        /// Gets the local TCP port number for the local endpoint to which the connection is established
        /// </summary>
        public int LocalPort { get; }

        #endregion

        /// <summary>
        /// Gets or sets the mode for the connection.  When a connection is in <see cref="ConnectionMode.Normal"/> mode,
        /// commands will be process by the global command resolution system.  When the connection is in <see cref="ConnectionMode.InteractiveProgram"/>,
        /// all input is directed to a running program that is serving the connection.
        /// </summary>
        private ConnectionMode Mode { get; set; } = ConnectionMode.Normal;

        #region IO and Connection Management
        public async void Process(CancellationToken cancellationToken = default(CancellationToken))
        {
            await this.SendAsync("200 Service available, posting allowed\r\n");

            Debug.Assert(this._stream != null, "The stream was 'null', but it should not have been because the connection was accepted and processing is beginning.");

            bool send403;

            try
            {
                while (true)
                {
                    if (!this._client.Connected || !this._client.Client.Connected) return;

                    if (!this._stream.CanRead)
                    {
                        await this.Shutdown();
                        return;
                    }

                    var bytesRead = await this._stream.ReadAsync(this._buffer, 0, BufferSize, cancellationToken);

                    // There  might be more data, so store the data received so far.
                    this._builder.Append(Encoding.ASCII.GetString(this._buffer, 0, bytesRead));

                    // Not all data received OR no more but not yet ending with the delimiter. Get more.
                    var content = this._builder.ToString();
                    if (bytesRead == BufferSize || !content.EndsWith("\r\n", StringComparison.Ordinal))
                    {
                        // Read some more.
                        continue;
                    }

                    // All the data has been read from the 
                    // client. Display it on the console.
                    if (this.ShowBytes && this.ShowData)
                        _Logger.TraceFormat(
                            "{0}:{1} >{2}> {3} bytes: {4}",
                            this.RemoteAddress, 
                            this.RemotePort, 
                            ">",
                            content.Length,
                            content.TrimEnd('\r', '\n'));
                    else if (this.ShowBytes)
                        _Logger.TraceFormat(
                            "{0}:{1} >{2}> {3} bytes",
                            this.RemoteAddress, 
                            this.RemotePort, 
                            ">",
                            content.Length);
                    else if (this.ShowData)
                        _Logger.TraceFormat(
                            "{0}:{1} >{2}> {3}",
                            this.RemoteAddress,
                            this.RemotePort,
                            ">",
                            content.TrimEnd('\r', '\n'));

                    if (this._inProcessCommand?.MessageHandler != null)
                    {
                        // Ongoing read - don't parse it for commands
                        this._inProcessCommand = await this._inProcessCommand.MessageHandler(content, this._inProcessCommand);
                        if (this._inProcessCommand != null && this._inProcessCommand.IsQuitting)
                            this._inProcessCommand = null;
                    }
                    else if (this.Mode == ConnectionMode.InteractiveProgram)
                    {
                        // Send input instead to the program bound to this connection
                        Debug.Assert(this._programInputHandler != null, "this._programInputHandler != null");
                        this._programInputHandler.Invoke(content);
                    }
                    else
                    {
                        var command = content.Split(' ').First().TrimEnd('\r', '\n').ToUpperInvariant();
                        if (_BuiltInCommandDirectory.ContainsKey(command))
                        {
                            try
                            {
                                if (this.ShowCommands)
                                    _Logger.TraceFormat("{0}:{1} >{2}> {3}", this.RemoteAddress, this.RemotePort, ">", content.TrimEnd('\r', '\n'));

                                var result = await _BuiltInCommandDirectory[command].Invoke(this, content);

                                if (!result.IsHandled)
                                    await this.SendAsync("500 Unknown command\r\n");
                                else if (result.MessageHandler != null)
                                    this._inProcessCommand = result;
                                else if (result.IsQuitting)
                                    return;
                            }
                            catch (Exception ex)
                            {
                                send403 = true;
                                _Logger.Error("Exception processing a command", ex);
                                break;
                            }
                        }
                        else
                        {
                            // Spawn the program as an asychronous task (no await) so input can still be processed on this connection
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            Task.Factory.StartNew(async () =>
                                {
                                    var context = await this._server.ScriptingEngine.RunProgramAsync<int>(command, this.Identity, this, cancellationToken);
                                    if (context.ErrorNumber == Scripting.ContextErrorNumber.ProgramNotFound || context.ErrorNumber == Scripting.ContextErrorNumber.ProgramNotSpecified)
                                        await this.SendAsync("Huh?\r\n");
                                    else if (context.ErrorNumber == Scripting.ContextErrorNumber.AuthenticationRequired)
                                        await this.SendAsync("You must be logged in to use that command.\r\n");
                                    else
                                        switch (context.State)
                                        {
                                            case Scripting.ContextState.Aborted:
                                                await this.SendAsync("Aborted.\r\n");
                                                break;
                                            case Scripting.ContextState.Errored:
                                                await this.SendAsync($"ERROR: {context.ErrorMessage}\r\n");
                                                break;
                                            case Scripting.ContextState.Killed:
                                                await this.SendAsync($"KILLED: {context.ErrorMessage}\r\n");
                                                break;
                                            case Scripting.ContextState.Loaded:
                                                await this.SendAsync($"STUCK: {context.ProgramName} loaded but not completed.\r\n");
                                                break;
                                            case Scripting.ContextState.Paused:
                                                await this.SendAsync($"Paused: {context.ProgramName}.\r\n");
                                                break;
                                            case Scripting.ContextState.Running:
                                                await this.SendAsync($"Running... {context.ProgramName}.\r\n");
                                                break;
                                            case Scripting.ContextState.Completed:
                                                // Write feedback to output
                                                if (context.Output.Count == 0)
                                                    await this.SendAsync($"{context.ProgramName} complete.  Result:{context.ReturnValue}\r\n");
                                                else
                                                    foreach (var line in context.Output)
                                                        await this.SendAsync($"{line}\r\n");
                                                break;
                                        }
                                },
                                cancellationToken);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        }
                    }

                    this._builder.Clear();
                }
            }
            catch (DecoderFallbackException dfe)
            {
                send403 = true;
                _Logger.Error("Decoder Fallback Exception socket " + this.RemoteAddress, dfe);
            }
            catch (IOException se)
            {
                send403 = true;
                _Logger.Error("I/O Exception on socket " + this.RemoteAddress, se);
            }
            catch (SocketException se)
            {
                send403 = true;
                _Logger.Error("Socket Exception on socket " + this.RemoteAddress, se);
            }
            catch (NotSupportedException nse)
            {
                _Logger.Error("Not Supported Exception", nse);
                return;
            }
            catch (ObjectDisposedException ode)
            {
                _Logger.Error("Object Disposed Exception", ode);
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
                await this._stream.WriteAsync(byteData, 0, byteData.Length);
                if (this.ShowBytes && this.ShowData)
                    _Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes: {5}",
                        this.RemoteAddress, 
                        this.RemotePort,
                        "<",
                        "<",
                        byteData.Length,
                        data.TrimEnd('\r', '\n'));
                else if (this.ShowBytes)
                    _Logger.TraceFormat(
                        "{0}:{1} <{2}{3} {4} bytes",
                        this.RemoteAddress, 
                        this.RemotePort, 
                        "<",
                        "<",
                        byteData.Length);
                else if (this.ShowData)
                    _Logger.TraceFormat(
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
                _Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
            catch (SocketException)
            {
                // Don't send 403 - the sending socket isn't working.
                _Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
            catch (ObjectDisposedException)
            {
                _Logger.VerboseFormat("{0}:{1} XXX CONNECTION TERMINATED", this.RemoteAddress, this.RemotePort);
                return false;
            }
        }

        public void RedirectInputToProgram([NotNull] Action<string> programInputHandler)
        {
            this._programInputHandler = programInputHandler;
            this.Mode = ConnectionMode.InteractiveProgram;
        }

        public void ResetInputRedirection()
        {
            this.Mode = ConnectionMode.Normal;
            this._programInputHandler = null;
        }

        public async Task Shutdown()
        {
            if (this._client.Connected)
            {
                await this.SendAsync("205 closing connection\r\n");
                this._client.Client?.Shutdown(SocketShutdown.Both);
                this._client.Close();
            }

            this._server.RemoveConnection(this);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Handles the CONNECT command from a client, which allows a client to authenticate against an existing
        /// player record for a username and a password
        /// </summary>
        /// <returns>A command processing result specifying the command is handled.</returns>
        private async Task<CommandProcessingResult> ConnectAsync(string data)
        {
            var match = System.Text.RegularExpressions.Regex.Match(data, @"(CONNECT|connect)\s+(?<username>[^\s]+)\s(?<password>[^\r\n]+)");
            if (!match.Success)
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #001\r\n");
                return new CommandProcessingResult(true);
            }

            var username = match.Groups["username"]?.Value;
            if (string.IsNullOrWhiteSpace(username))
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #002\r\n");
                return new CommandProcessingResult(true);
            }

            var playerRef = (DbRef)await this._server.ScriptingEngine.Redis.HashGetAsync<string>("mudpie::usernames", username.ToLowerInvariant());
            if (DbRef.NOTHING.Equals(playerRef))
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #003\r\n");
                return new CommandProcessingResult(true);
            }

            var player = Player.Get(this._server.ScriptingEngine.Redis, playerRef);
            if (player == null)
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #004\r\n");
                return new CommandProcessingResult(true);
            }

            var password = match.Groups["password"]?.Value;
            if (password == null)
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #005\r\n");
                return new CommandProcessingResult(true);
            }

            var secureAttempt = new SecureString();
            foreach (var c in password)
                secureAttempt.AppendChar(c);

            var passwordMatch = player.VerifyPassword(secureAttempt);
            if (!passwordMatch)
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #006\r\n");
                return new CommandProcessingResult(true);
            }

            this.Identity = player;
            await this.SendAsync("Greetings, Professor Faulkin\r\n");
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

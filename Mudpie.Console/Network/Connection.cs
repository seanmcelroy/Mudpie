// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Connection.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
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
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Mudpie.Scripting.Common;
    using Mudpie.Server.Data;

    using Program = Mudpie.Console.Program;

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
        private static readonly Dictionary<string, Func<Connection, string, CancellationToken, Task<CommandProcessingResult>>> BuiltInCommandDirectory;

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
        /// The handler that receives messages while the <see cref="Mode"/> is set to <see cref="ConnectionMode.InteractiveProgram"/>
        /// </summary>
        [CanBeNull]
        private Action<string> programInputHandler;

        /// <summary>
        /// Initializes static members of the <see cref="Connection"/> class.
        /// </summary>
        static Connection()
        {
            BuiltInCommandDirectory = new Dictionary<string, Func<Connection, string, CancellationToken, Task<CommandProcessingResult>>>
                {
                    { "CONNECT", async (c, data, token) => await c.ConnectAsync(data, token) }
                };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Connection"/> class.
        /// </summary>
        /// <param name="server">The server instance that owns this connection</param>
        /// <param name="client">The <see cref="TcpClient"/> that accepted this connection</param>
        /// <param name="stream">The <see cref="Stream"/> from the <paramref name="client"/></param>
        public Connection(
            [NotNull] Server server,
            [NotNull] TcpClient client,
            [NotNull] Stream stream)
        {
            this.client = client;
            this.client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            this.ShowBytes = server.ShowBytes;
            this.ShowCommands = server.ShowCommands;
            this.ShowData = server.ShowData;
            this.server = server;
            this.stream = stream;

            var remoteIpEndpoint = (IPEndPoint)this.client.Client.RemoteEndPoint;
            Debug.Assert(remoteIpEndpoint != null, "remoteIpEndpoint != null");
            Debug.Assert(remoteIpEndpoint.Address != null, "remoteIpEndpoint.Address != null");
            this.RemoteAddress = remoteIpEndpoint.Address;
            this.RemotePort = remoteIpEndpoint.Port;
            var localIpEndpoint = (IPEndPoint)this.client.Client.LocalEndPoint;
            Debug.Assert(localIpEndpoint != null, "localIpEndpoint != null");
            Debug.Assert(localIpEndpoint.Address != null, "localIpEndpoint.Address != null");
            this.LocalAddress = localIpEndpoint.Address;
            this.LocalPort = localIpEndpoint.Port;
        }

        #region Authentication

        /// <summary>
        /// Gets the identity of the player on this connection, if the user has authenticated with the CONNECT command
        /// </summary>
        [CanBeNull]
        public Player Identity { get; private set; }
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

        /// <summary>
        /// Gets a value indicating whether the byte transmitted counts are logged to the logging instance
        /// </summary>
        private bool ShowBytes { get; }

        /// <summary>
        /// Gets a value indicating whether the commands transmitted are logged to the logging instance
        /// </summary>
        private bool ShowCommands { get; }

        /// <summary>
        /// Gets a value indicating whether the actual bytes (data) transmitted are logged to the logging instance
        /// </summary>
        private bool ShowData { get; }

        #region IO and Connection Management

        /// <summary>
        /// Processing loop to handle incoming bytes on a connection
        /// </summary>
        /// <param name="cancellationToken">A cancellation token used to abort the processing loop</param>
        public async void Process(CancellationToken cancellationToken)
        {
            await this.SendAsync("200 Service available, posting allowed\r\n", cancellationToken);

            Debug.Assert(this.stream != null, "The stream was 'null', but it should not have been because the connection was accepted and processing is beginning.");

            try
            {
                while (true)
                {
                    if (!this.client.Connected || !this.client.Client.Connected)
                    {
                        return;
                    }

                    if (!this.stream.CanRead)
                    {
                        await this.ShutdownAsync();
                        return;
                    }

                    var bytesRead = await this.stream.ReadAsync(this.buffer, 0, BufferSize, cancellationToken);

                    // There  might be more data, so store the data received so far.
                    this.builder.Append(Encoding.ASCII.GetString(this.buffer, 0, bytesRead));

                    // Not all data received OR no more but not yet ending with the delimiter. Get more.
                    var content = this.builder.ToString();
                    if (bytesRead == BufferSize || !content.EndsWith("\r\n", StringComparison.Ordinal))
                    {
                        // Read some more.
                        continue;
                    }

                    // Clear builder FIRST in case another command comes in while executing.
                    this.builder.Clear();

                    // All the data has been read from the 
                    // client. Display it on the console.
                    if (this.ShowBytes && this.ShowData)
                    {
                        Logger.TraceFormat(
                            "{0}:{1}{2} >{3}> {4} bytes: {5}",
                            this.RemoteAddress,
                            this.RemotePort,
                            this.Identity == null ? null : $"({this.Identity.Username})",
                            ">",
                            content.Length,
                            content.TrimEnd('\r', '\n'));
                    }
                    else if (this.ShowBytes)
                    {
                        Logger.TraceFormat(
                            "{0}:{1}{2} >{3}> {4} bytes",
                            this.RemoteAddress,
                            this.RemotePort,
                            this.Identity == null ? null : $"({this.Identity.Username})",
                            ">",
                            content.Length);
                    }
                    else if (this.ShowData)
                    {
                        Logger.TraceFormat(
                            "{0}:{1}{2} >{3}> {4}",
                            this.RemoteAddress,
                            this.RemotePort,
                            this.Identity == null ? null : $"({this.Identity.Username})",
                            ">",
                            content.TrimEnd('\r', '\n'));
                    }

                    if (this.Mode == ConnectionMode.InteractiveProgram)
                    {
                        // Send input instead to the program bound to this connection
                        Debug.Assert(this.programInputHandler != null, "this._programInputHandler != null");
                        this.programInputHandler.Invoke(content);
                    }
                    else if (await this.ProcessMessageAsync(content, cancellationToken))
                    {
                        await this.ShutdownAsync();
                        return;
                    }
                }
            }
            catch (DecoderFallbackException dfe)
            {
                Logger.Error("Decoder Fallback Exception socket " + this.RemoteAddress, dfe);
            }
            catch (IOException se)
            {
                Logger.Error("I/O Exception on socket " + this.RemoteAddress, se);
            }
            catch (SocketException se)
            {
                Logger.Error("Socket Exception on socket " + this.RemoteAddress, se);
            }
            catch (NotSupportedException nse)
            {
                Logger.Error("Not Supported Exception", nse);
            }
            catch (ObjectDisposedException ode)
            {
                Logger.Error("Object Disposed Exception", ode);
            }
        }

        /// <summary>
        /// Sends the formatted data to the client
        /// </summary>
        /// <param name="format">The data, or format string for data, to send to the client</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <param name="args">The argument applied as a format string to <paramref name="format"/> to create the data to send to the client</param>
        /// <returns>A value indicating whether or not the transmission was successful</returns>
        [StringFormatMethod("format"), NotNull]
        internal async Task<bool> SendAsync([NotNull] string format, CancellationToken cancellationToken, [NotNull] params object[] args)
        {
            return await this.SendInternalAsync(string.Format(CultureInfo.InvariantCulture, format, args), cancellationToken);
        }
        
        /// <summary>
        /// Redirects input from the connection to a <see cref="Program"/> input handler, running in a <see cref="Scripting.Engine"/>
        /// </summary>
        /// <param name="inputHandler">The input handler that receives the redirect <see cref="Player"/> input</param>
        internal void RedirectInputToProgram([NotNull] Action<string> inputHandler)
        {
            this.programInputHandler = inputHandler;
            this.Mode = ConnectionMode.InteractiveProgram;
        }

        /// <summary>
        /// Returns input back to the normal message <see cref="Process"/> loop
        /// </summary>
        internal void ResetInputRedirection()
        {
            this.Mode = ConnectionMode.Normal;
            this.programInputHandler = null;
        }

        /// <summary>
        /// Permanently terminates the connection
        /// </summary>
        /// <returns>A task object used for asynchronous process</returns>
        [NotNull]
        internal async Task ShutdownAsync()
        {
            if (this.client.Connected)
            {
                var cts = new CancellationTokenSource(5000); // 5 seconds
                await this.SendAsync("GOODBYE!\r\n", cts.Token);
                this.client.Client?.Shutdown(SocketShutdown.Both);
                this.client.Close();
                this.client.Dispose();
            }

            this.server.RemoveConnection(this);
        }

        /// <summary>
        /// Processing subroutine to handle incoming messages from a connection's <see cref="Process"/> command
        /// </summary>
        /// <param name="content">The message received from the <see cref="Process"/> message handling loop</param>
        /// <param name="cancellationToken">A cancellation token used to abort the processing loop</param>
        /// <returns>A value indicating whether or not the parent message loop should quit</returns>
        private async Task<bool> ProcessMessageAsync(
            string content,
            CancellationToken cancellationToken)
        {
            if (BuiltInCommandDirectory.ContainsKey(content.Split(' ').First().TrimEnd('\r', '\n').ToUpperInvariant()))
            {
                try
                {
                    if (this.ShowCommands)
                    {
                        Logger.TraceFormat(
                            "{0}:{1} >{2}> {3}",
                            this.RemoteAddress,
                            this.RemotePort,
                            ">",
                            content.TrimEnd('\r', '\n'));
                    }

                    var result =
                        await
                            BuiltInCommandDirectory[content.Split(' ').First().TrimEnd('\r', '\n').ToUpperInvariant()]
                                .Invoke(this, content, cancellationToken);

                    if (!result.IsHandled)
                    {
                        await this.SendAsync("500 Unknown command\r\n", cancellationToken);
                    }
                    else if (result.IsQuitting)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Exception processing a command", ex);
                    return false;
                }
            }
            else
            {
                var wordMatches = Regex.Matches(content, @"([^\s]*(""[^""]*"")[^\s]*)|([^\s""]+)");
                if (wordMatches.Count < 1)
                {
                    await this.SendAsync("What?\r\n", cancellationToken);
                    return false;
                }

                var words = wordMatches.Cast<Match>().Select(m => m.Groups[0].Value).ToArray();
                var verb = words.ElementAtOrDefault(0);
                var argString = verb == null ? null : content.TrimStart().Substring(verb.Trim().Length).Trim();

                Debug.Assert(!string.IsNullOrWhiteSpace("verb"), "Verb not specified");

                for (var i = 1; i < words.Length; i++)
                {
                    words[i] = words[i].Replace(@"\u001", string.Empty).Replace("\"", string.Empty);
                }

                var prepositions = new[]
                                       {
                                           "with", "using", "at", "to", "in front of", "in", "inside", "into",
                                           "on top of", "on", "onto", "upon", "out of", "from inside", "from", "over",
                                           "through", "under", "underneath", "beneath", "behind", "beside", "for",
                                           "about", "as", "off", "off of"
                                       };

                string prep = null;
                var prepFoundStart = -1;
                foreach (var word in words.Skip(1).Where(w => w != null))
                {
                    var prepMatch = prepositions
                        .Select(p => Regex.Match(word, @"(\b" + Regex.Escape(p) + @"\b|\=)", RegexOptions.IgnoreCase))
                        .FirstOrDefault(p => p != null && p.Success);

                    if (verb != null && prepMatch != null)
                    {
                        prep = word.IndexOf("=", StringComparison.Ordinal) > -1 ? "=" : word;
                        prepFoundStart = content.IndexOf(prep, verb.Length, StringComparison.Ordinal);
                    }

                    if (prep != null)
                    {
                        break;
                    }
                }

                string indirectObject = null;
                string directObject = null;
                if (prep != null)
                {
                    directObject =
                        content.Substring(verb.Length, prepFoundStart - verb.Length)
                            .Replace(@"\u001", string.Empty)
                            .Replace("\"", string.Empty)
                            .Trim();
                    indirectObject =
                        content.Substring(prepFoundStart + prep.Length)
                            .Replace(@"\u001", string.Empty)
                            .Replace("\"", string.Empty)
                            .Trim();

                    // For formats like 'look at chair' rather than 'look chair'
                    if (string.IsNullOrWhiteSpace(directObject) && !string.IsNullOrWhiteSpace(indirectObject))
                    {
                        directObject = indirectObject;
                        indirectObject = null;
                    }
                }
                else if (verb != null)
                {
                    directObject =
                        content.Substring(verb.Length)
                            .Replace(@"\u001", string.Empty)
                            .Replace("\"", string.Empty)
                            .Trim();
                }

                Logger.Verbose(
                    $"{content.TrimEnd('\r', '\n')} => VERB: {verb}, DO: {directObject}, PREP: {prep}, IO: {indirectObject}");

                var directObjectReferenceAndObject = directObject == null
                                                ? new Tuple<DbRef, ObjectBase>(DbRef.FailedMatch, null)
                                                : await
                                                      MatchUtility.MatchObjectAsync(
                                                          this.Identity,
                                                          this.server.ScriptingEngine.Redis,
                                                          directObject,
                                                          cancellationToken);
                Logger.Verbose($"{directObject} => REF: {directObjectReferenceAndObject.Item1}");
                var indirectObjectReferenceAndObject = indirectObject == null
                                                  ? new Tuple<DbRef, ObjectBase>(DbRef.FailedMatch, null)
                                                  : await
                                                        MatchUtility.MatchObjectAsync(
                                                            this.Identity,
                                                            this.server.ScriptingEngine.Redis,
                                                            indirectObject,
                                                            cancellationToken);
                Logger.Verbose($"{indirectObject} => REF: {indirectObjectReferenceAndObject.Item1}");
                var verbReferenceAndObject = verb == null
                                        ? new Tuple<DbRef, ObjectBase>(DbRef.Nothing, null)
                                        : await
                                              MatchUtility.MatchVerbAsync(
                                                  this.Identity,
                                                  this.server.ScriptingEngine.Redis,
                                                  verb,
                                                  directObjectReferenceAndObject.Item1,
                                                  indirectObjectReferenceAndObject.Item1,
                                                  cancellationToken);
                Logger.Verbose($"{verb} => REF: {verbReferenceAndObject.Item1}");

                if (verbReferenceAndObject.Item1.Equals(DbRef.Ambiguous))
                {
                    await this.SendAsync("Which one?\r\n", cancellationToken);
                    return false;
                }

                if (verbReferenceAndObject.Item1.Equals(DbRef.FailedMatch))
                {
                    await this.SendAsync("Er?\r\n", cancellationToken);
                    return false;
                }

                var link = verbReferenceAndObject.Item2 as Link;
                Debug.Assert(link != null, "link != null");

                var target = await ObjectBase.GetAsync(this.server.ScriptingEngine.Redis, link.Target, cancellationToken);
                if (target == null)
                {
                    await this.SendAsync("You peer closer and notice a rip in the space-time continuum...\r\n", cancellationToken);
                    return false;
                }

                // NOTE: This means no unauthenticated programs can run.. but we want 'caller' not to be null
                if (target is Mudpie.Server.Data.Program && this.Identity != null)
                {
                    // Spawn the program as an asychronous task (no await) so input can still be processed on this connection
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    Task.Factory.StartNew(
                        async () =>
                            {
                                var context =
                                    await
                                        this.server.ScriptingEngine.RunProgramAsync<object>(
                                            target.DbRef,
                                            this,
                                            verbReferenceAndObject.Item2,
                                            this.Identity,
                                            verb,
                                            argString,
                                            words.Skip(1).ToArray(),
                                            directObject,
                                            directObjectReferenceAndObject.Item2,
                                            prep,
                                            indirectObject,
                                            indirectObjectReferenceAndObject.Item2,
                                            cancellationToken);
                                if (context.ErrorNumber == Scripting.ContextErrorNumber.ProgramNotFound
                                    || context.ErrorNumber == Scripting.ContextErrorNumber.ProgramNotSpecified)
                                {
                                    await this.SendAsync("Huh?\r\n\r\n", cancellationToken);
                                }
                                else if (context.ErrorNumber == Scripting.ContextErrorNumber.AuthenticationRequired)
                                {
                                    await this.SendAsync("You must be logged in to use that command.\r\n\r\n", cancellationToken);
                                }
                                else
                                {
                                    switch (context.State)
                                    {
                                        case Scripting.ContextState.Aborted:
                                            await this.SendAsync("Aborted.\r\n", cancellationToken);
                                            break;
                                        case Scripting.ContextState.Errored:
                                            await this.SendAsync($"ERROR: {context.ErrorMessage}\r\n", cancellationToken);
                                            break;
                                        case Scripting.ContextState.Killed:
                                            await this.SendAsync($"KILLED: {context.ErrorMessage}\r\n", cancellationToken);
                                            break;
                                        case Scripting.ContextState.Loaded:
                                            await
                                                this.SendAsync($"STUCK: {context.ProgramName} loaded but not completed.\r\n", cancellationToken);
                                            break;
                                        case Scripting.ContextState.Paused:
                                            await this.SendAsync($"Paused: {context.ProgramName}.\r\n", cancellationToken);
                                            break;
                                        case Scripting.ContextState.Running:
                                            await this.SendAsync($"Running... {context.ProgramName}.\r\n", cancellationToken);
                                            break;
                                        case Scripting.ContextState.Completed:
                                            Logger.Verbose(
                                                $"{this.Identity?.Name}> Run of {context.ProgramName} complete.  Result:{context.ReturnValue}\r\n");
                                            break;
                                    }
                                }
                            },
                        cancellationToken);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }

            return false;
        }

        /// <summary>
        /// Sends the formatted data to the client
        /// </summary>
        /// <param name="data">The data to send to the client</param>
        /// <returns>A value indicating whether or not the transmission was successful</returns>
        /// <param name="cancellationToken">A cancellation token used to abort the processing loop</param>
        [NotNull]
        private async Task<bool> SendInternalAsync([NotNull] string data, CancellationToken cancellationToken)
        {
            // Convert the string data to byte data using ASCII encoding.
            var byteData = Encoding.UTF8.GetBytes(data);

            try
            {
                // Begin sending the data to the remote device.
                await this.stream.WriteAsync(byteData, 0, byteData.Length, cancellationToken);
                if (this.ShowBytes && this.ShowData)
                {
                    Logger.TraceFormat(
                        "{0}:{1}{2} <{3}{4} {5} bytes: {6}",
                        this.RemoteAddress,
                        this.RemotePort,
                        this.Identity == null ? null : $"({this.Identity.Username})",
                        "<",
                        "<",
                        byteData.Length,
                        data.TrimEnd('\r', '\n'));
                }
                else if (this.ShowBytes)
                {
                    Logger.TraceFormat(
                        "{0}:{1}{2} <{3}{4} {5} bytes",
                        this.RemoteAddress,
                        this.RemotePort,
                        this.Identity == null ? null : $"({this.Identity.Username})",
                        "<",
                        "<",
                        byteData.Length);
                }
                else if (this.ShowData)
                {
                    Logger.TraceFormat(
                        "{0}:{1}{2} <{3}{4} {5}",
                        this.RemoteAddress,
                        this.RemotePort,
                        this.Identity == null ? null : $"({this.Identity.Username})",
                        "<",
                        "<",
                        data.TrimEnd('\r', '\n'));
                }

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
        #endregion

        #region Commands

        /// <summary>
        /// Handles the CONNECT command from a client, which allows a client to authenticate against an existing
        /// player record for a username and a password
        /// </summary>
        /// <param name="data">The full data line received from the client</param>
        /// <param name="cancellationToken">A cancellation token used to abort the processing loop</param>
        /// <returns>A command processing result specifying the command is handled.</returns>
        [NotNull, ItemNotNull]
        private async Task<CommandProcessingResult> ConnectAsync([NotNull] string data, CancellationToken cancellationToken)
        {
            var match = Regex.Match(data, @"connect\s+(?<username>[^\s]+)\s(?<password>[^\r\n]+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #001\r\n", cancellationToken);
                return new CommandProcessingResult(true);
            }

            var username = match.Groups["username"]?.Value;
            if (string.IsNullOrWhiteSpace(username))
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #002\r\n", cancellationToken);
                return new CommandProcessingResult(true);
            }

            var playerRef = (DbRef)await this.server.ScriptingEngine.Redis.HashGetAsync<string>("mudpie::usernames", username.ToLowerInvariant());
            if (DbRef.Nothing.Equals(playerRef))
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #003\r\n", cancellationToken);
                return new CommandProcessingResult(true);
            }

            var player = await Player.GetAsync(this.server.ScriptingEngine.Redis, playerRef, cancellationToken);
            if (player == null)
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #004\r\n", cancellationToken);
                return new CommandProcessingResult(true);
            }

            var password = match.Groups["password"]?.Value;
            if (password == null)
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #005\r\n", cancellationToken);
                return new CommandProcessingResult(true);
            }

            var secureAttempt = new SecureString();
            foreach (var c in password)
            {
                secureAttempt.AppendChar(c);
            }

            var passwordMatch = player.VerifyPassword(secureAttempt);
            if (!passwordMatch)
            {
                await this.SendAsync("Either that player does not exist, or has a different password. #006\r\n", cancellationToken);
                return new CommandProcessingResult(true);
            }

            this.Identity = player;
            this.Identity.LastLogin = DateTime.UtcNow;
            await this.Identity.SaveAsync(this.server.ScriptingEngine.Redis, cancellationToken);

            await this.SendAsync("Greetings, Professor Faulkin\r\n", cancellationToken);
            return new CommandProcessingResult(true);
        }

        #endregion
    }
}

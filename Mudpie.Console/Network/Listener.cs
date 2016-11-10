namespace Mudpie.Console.Network
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;

    using JetBrains.Annotations;

    using log4net;

    internal class Listener : TcpListener
    {
        // Thread signal.
        private readonly Server server;
        private static readonly ILog _Logger = LogManager.GetLogger(typeof(Listener));

        public Listener([NotNull] Server server, [NotNull] IPEndPoint localEp)
            : base(localEp)
        {
            this.server = server;
        }

        public PortClass PortType { get; set; }

        public async void StartAccepting()
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
                        continue;

                    // Create the state object.
                    Connection connection;

                    if (this.PortType == PortClass.ClearText)
                    {
                        var stream = handler.GetStream();

                        connection = new Connection(this.server, handler, stream);
                    }
                    else
                    {
                        var stream = handler.GetStream();
                        var sslStream = new SslStream(stream);

                        try
                        {
                            await sslStream.AuthenticateAsServerAsync(this.server.ServerAuthenticationCertificate);
                        }
                        catch (IOException ioe)
                        {
                            _Logger.Error("I/O Exception attempting to perform TLS handshake", ioe);
                            return;
                        }

                        connection = new Connection(this.server, handler, sslStream, true);
                    }

                    this.server.AddConnection(connection);

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

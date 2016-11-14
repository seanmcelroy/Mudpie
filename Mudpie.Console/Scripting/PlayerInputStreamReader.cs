// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PlayerInputStreamReader.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A special type of stream reader used to capture player input for a script,
//   blocking until the user has supplied the required text in the case of <see cref="ReadLine" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Scripting
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    /// <summary>
    /// A special type of stream reader used to capture player input for a script,
    /// blocking until the user has supplied the required text in the case of <see cref="ReadLine"/>
    /// </summary>
    public class PlayerInputStreamReader : StreamReader
    {
        /// <summary>
        /// The wait handle used to block on the entering of a CRLF from the player
        /// </summary>
        [NotNull]
        private readonly AutoResetEvent _waitHandle = new AutoResetEvent(false);

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerInputStreamReader"/> class.
        /// </summary>
        /// <param name="stream">The stream to be read</param>
        internal PlayerInputStreamReader([NotNull] MemoryStream stream) : base(stream)
        {
        }

        /// <inheritdoc />
        public override int Read()
        {
            this._waitHandle.WaitOne();
            var ret = base.Read();
            this._waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override int Read(char[] buffer, int index, int count)
        {
            this._waitHandle.WaitOne();
            var ret = base.Read(buffer, index, count);
            this._waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            this._waitHandle.WaitOne();
            var ret = await base.ReadAsync(buffer, index, count);
            this._waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override int ReadBlock(char[] buffer, int index, int count)
        {
            this._waitHandle.WaitOne();
            var ret = base.ReadBlock(buffer, index, count);
            this._waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override async Task<int> ReadBlockAsync(char[] buffer, int index, int count)
        {
            this._waitHandle.WaitOne();
            var ret = await base.ReadBlockAsync(buffer, index, count);
            this._waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override string ReadLine()
        {
            this.BaseStream.Position = 0;
            this._waitHandle.WaitOne();
            var ret = base.ReadLine();
            this._waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override async Task<string> ReadLineAsync()
        {
            this._waitHandle.WaitOne();
            var ret = await base.ReadLineAsync();
            this._waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override string ReadToEnd()
        {
            this._waitHandle.WaitOne();
            var ret = base.ReadToEnd();
            this._waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override async Task<string> ReadToEndAsync()
        {
            this._waitHandle.WaitOne();
            var ret = await base.ReadToEndAsync();
            this._waitHandle.Reset();
            return ret;
        }

        /// <summary>
        /// Notifies this stream reader the underlying stream has changed in another thread
        /// </summary>
        /// <param name="encoding">The encoding used to write data into the underlying stream in another thread</param>
        internal void NotifyStreamChanged([NotNull] Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            if (this.BaseStream.Length < 2)
                return;

            var bytes = ((MemoryStream)this.BaseStream).ToArray();
            var str = encoding.GetString(bytes);
            if (str.IndexOf("\r\n", StringComparison.Ordinal) > -1)
                this._waitHandle.Set();
            else
                throw new InvalidOperationException();
        }
    }
}

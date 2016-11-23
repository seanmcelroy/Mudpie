// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PlayerInputStreamReader.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A special type of stream reader used to capture player input for a script,
//   blocking until the user has supplied the required text in the case of <see cref="ReadLine" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Scripting.Common
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
        private readonly AutoResetEvent waitHandle = new AutoResetEvent(false);

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
            this.waitHandle.WaitOne();
            var ret = base.Read();
            this.waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override int Read(char[] buffer, int index, int count)
        {
            this.waitHandle.WaitOne();
            var ret = base.Read(buffer, index, count);
            this.waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            this.waitHandle.WaitOne();
            // ReSharper disable once PossibleNullReferenceException
            var ret = await base.ReadAsync(buffer, index, count);
            this.waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override int ReadBlock(char[] buffer, int index, int count)
        {
            this.waitHandle.WaitOne();
            var ret = base.ReadBlock(buffer, index, count);
            this.waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override async Task<int> ReadBlockAsync(char[] buffer, int index, int count)
        {
            this.waitHandle.WaitOne();
            var ret = await base.ReadBlockAsync(buffer, index, count);
            this.waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override string ReadLine()
        {
            this.BaseStream.Position = 0;
            this.waitHandle.WaitOne();
            var ret = base.ReadLine();
            this.waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override async Task<string> ReadLineAsync()
        {
            this.waitHandle.WaitOne();
            var ret = await base.ReadLineAsync();
            this.waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override string ReadToEnd()
        {
            this.waitHandle.WaitOne();
            var ret = base.ReadToEnd();
            this.waitHandle.Reset();
            return ret;
        }

        /// <inheritdoc />
        public override async Task<string> ReadToEndAsync()
        {
            this.waitHandle.WaitOne();
            var ret = await base.ReadToEndAsync();
            this.waitHandle.Reset();
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
                this.waitHandle.Set();
            else
                throw new InvalidOperationException();
        }
    }
}

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PlayerInputStreamWriter.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   Defines the PlayerInputStreamWriter type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Scripting
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using JetBrains.Annotations;

    /// <summary>
    /// A special stream writer that notifies a script waiting with a <see cref="PlayerInputStreamReader"/> when this
    /// writer receives data from the player
    /// </summary>
    public class PlayerInputStreamWriter : StreamWriter
    {
        /// <summary>
        /// The reader stream waiting on the input provided by this writer
        /// </summary>
        [NotNull]
        private readonly PlayerInputStreamReader readerStream;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlayerInputStreamWriter"/> class.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="readerStream">The reader stream waiting on the input provided by this writer</param>
        internal PlayerInputStreamWriter([NotNull] MemoryStream stream, [NotNull] PlayerInputStreamReader readerStream)
            : base(stream)
        {
            this.readerStream = readerStream;
        }

        internal PlayerInputStreamWriter([NotNull] MemoryStream stream, [NotNull] Encoding encoding, [NotNull] PlayerInputStreamReader readerStream)
            : base(stream, encoding)
        {
            this.readerStream = readerStream;
        }

        internal PlayerInputStreamWriter([NotNull] MemoryStream stream, [NotNull] Encoding encoding, int bufferSize, [NotNull] PlayerInputStreamReader readerStream)
            : base(stream, encoding, bufferSize)
        {
            this.readerStream = readerStream;
        }

        internal PlayerInputStreamWriter([NotNull] MemoryStream stream, [NotNull] Encoding encoding, int bufferSize, bool leaveOpen, [NotNull] PlayerInputStreamReader readerStream)
            : base(stream, encoding, bufferSize, leaveOpen)
        {
            this.readerStream = readerStream;
        }

        /// <inheritdoc />
        public override void Write(char value)
        {
            base.Write(value);
            var ba = this.Encoding.GetBytes(new[] { value }, 0, 1);
            ((MemoryStream)this.readerStream.BaseStream).Write(ba, 0, 1);
            ((MemoryStream)this.readerStream.BaseStream).Position -= 1;
            this.readerStream.NotifyStreamChanged(this.Encoding);
        }

        /// <inheritdoc />
        public override void Write(string value)
        {
            var currentPosition = this.BaseStream.Position;
            base.Write(value);
            this.Flush();
            var newPosition = this.BaseStream.Position;
            var ba = ((MemoryStream)this.BaseStream).ToArray();
            ((MemoryStream)this.readerStream.BaseStream).Write(ba, (int)currentPosition, (int)newPosition - (int)currentPosition);
            ((MemoryStream)this.readerStream.BaseStream).Position -= newPosition - currentPosition;
            
            // NOTIFY HAPPENS FROM ASYNC VERSION
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string value)
        {
            // CALLS Write(string value) .. but we need to notify on this thread.
            await base.WriteAsync(value);
            this.readerStream.NotifyStreamChanged(this.Encoding);
        }

        /// <inheritdoc />
        public override void WriteLine()
        {
            var currentPosition = this.BaseStream.Position;
            base.WriteLine();
            this.Flush();
            var newPosition = this.BaseStream.Position;
            var ba = ((MemoryStream)this.BaseStream).ToArray();
            ((MemoryStream)this.readerStream.BaseStream).Write(ba, (int)currentPosition, (int)newPosition - (int)currentPosition);
            ((MemoryStream)this.readerStream.BaseStream).Position -= newPosition - currentPosition;
            this.readerStream.NotifyStreamChanged(this.Encoding);
        }

        /// <inheritdoc />
        public override void WriteLine(string value)
        {
            var currentPosition = this.BaseStream.Position;
            base.WriteLine(value);
            this.Flush();
            var newPosition = this.BaseStream.Position;
            var ba = ((MemoryStream)this.BaseStream).ToArray();
            ((MemoryStream)this.readerStream.BaseStream).Write(ba, (int)currentPosition, (int)newPosition - (int)currentPosition);
            ((MemoryStream)this.readerStream.BaseStream).Position -= newPosition - currentPosition;
            this.readerStream.NotifyStreamChanged(this.Encoding);
        }

        /// <inheritdoc />
        public override Task WriteLineAsync(string value)
        {
            // CALLS WriteLine(string value) .. but we need to notify on this thread.
            // Abandoned for now due to race conditions.
            throw new NotImplementedException();
        }
    }
}

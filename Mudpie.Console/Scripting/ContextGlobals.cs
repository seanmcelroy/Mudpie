// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ContextGlobals.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   Global context variables passed into a program execution context
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Scripting
{
    using System.IO;

    using JetBrains.Annotations;

    /// <summary>
    /// Global context variables passed into a program execution context
    /// </summary>
    [PublicAPI]
    public class ContextGlobals
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ContextGlobals"/> class.
        /// </summary>
        public ContextGlobals()
        {
            this.PlayerInput = new PlayerInputStreamReader(this.PlayerInputStreamInternal);
            this.PlayerInputWriterInternal = new PlayerInputStreamWriter(this.PlayerInputWriterStreamInternal, this.PlayerInput);
        }

        /// <summary>
        /// Gets global variables provided by the running engine, global to the whole MUD instance
        /// </summary>
        public object EngineGlobals { get; internal set; }

        /// <summary>
        /// Gets or sets the unique identifier of the <see cref="Data.ObjectBase"/> that triggered the program execution
        /// </summary>
        public string TriggerId { get; internal set; }

        /// <summary>
        /// Gets or sets the type of the <see cref="Data.ObjectBase"/> that triggered the program execution
        /// </summary>
        /// <example>
        /// Sample values are PLAYER and EXIT
        /// </example>
        public string TriggerType { get; internal set; }

        /// <summary>
        /// Gets or sets the name of the <see cref="Data.ObjectBase"/> that triggered the program execution
        /// </summary>
        public string TriggerName { get; internal set; }

        /// <summary>
        /// Gets or sets a text writer that can be used to send information back to the triggering object.
        /// </summary>
        [CanBeNull]
        public TextWriter PlayerOutput { get; internal set; }

        /// <summary>
        /// Gets a <see cref="StreamWriter"/> that is used by the internal <see cref="Network.Connection"/> to store input received by a player
        /// </summary>
        [NotNull]
        public PlayerInputStreamWriter PlayerInputWriterInternal { get; private set; }

        /// <summary>
        /// Gets a <see cref="StreamReader"/> that can be used by the script to retrieve text from the player waiting for it to read in
        /// </summary>
        [NotNull]
        public PlayerInputStreamReader PlayerInput { get; private set; }

        /// <summary>
        /// Gets a memory stream that stores text from the player that is waiting for transfer to the <see cref="PlayerInputStreamInternal"/> script-attached stream
        /// </summary>
        [NotNull]
        private MemoryStream PlayerInputWriterStreamInternal { get; } = new MemoryStream(2048);

        /// <summary>
        /// Gets a memory stream that stores text waiting for the script to read in
        /// </summary>
        [NotNull]
        private MemoryStream PlayerInputStreamInternal { get; } = new MemoryStream(2048);
    }
}

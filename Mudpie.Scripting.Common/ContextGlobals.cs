// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ContextGlobals.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   Global context variables passed into a program execution context
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Scripting.Common
{
    using System;
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
        /// <param name="thisObject">
        /// The object on which the verb for the command was found
        /// </param>
        /// <param name="caller">
        /// The object on which the verb that called the currently-running
        /// verb was found. For the first verb called for a given command, 'caller' has the
        /// same value as <see cref="Player"/>.
        /// </param>
        /// <param name="playerOutput">
        /// A text writer that can be used to send information back to the triggering object.
        /// </param>
        /// <param name="databaseLibrary">
        /// A library class that exposes functions to the script that allow it to modify objects in the database
        /// </param>
        public ContextGlobals([NotNull] IObjectBase thisObject, [CanBeNull] IObjectBase caller, [NotNull] TextWriter playerOutput, [NotNull] IDatabaseLibrary databaseLibrary)
        {
            if (thisObject == null)
            {
                throw new ArgumentNullException(nameof(thisObject));
            }

            if (caller == null)
            {
                throw new ArgumentNullException(nameof(caller));
            }

            if (playerOutput == null)
            {
                throw new ArgumentNullException(nameof(playerOutput));
            }

            if (databaseLibrary == null)
            {
                throw new ArgumentNullException(nameof(databaseLibrary));
            }

            this.This = thisObject;
            this.Caller = caller;
            this.PlayerInput = new PlayerInputStreamReader(this.PlayerInputStreamInternal);
            this.PlayerInputWriterInternal = new PlayerInputStreamWriter(this.PlayerInputWriterStreamInternal, this.PlayerInput);
            this.PlayerOutput = playerOutput;
            this.DatabaseLibrary = databaseLibrary;
        }

        /// <summary>
        /// Gets or sets the player who typed the command
        /// </summary>
        [CanBeNull, PublicAPI]
        public IObjectBase Player { get; set; }

        /// <summary>
        /// Gets or sets the location of the triggering object
        /// </summary>
        [CanBeNull, PublicAPI]
        public IObjectBase PlayerLocation { get; set; }

        /// <summary>
        /// Gets or sets the object on which the verb for the command was found
        /// </summary>
        [NotNull, PublicAPI]
        public IObjectBase This { get; set; }

        /// <summary>
        /// Gets the caller, the object on which the verb that called the currently-running
        /// verb was found. For the first verb called for a given command, 'caller' has the
        /// same value as <see cref="Player"/>.
        /// </summary>
        [CanBeNull, PublicAPI]
        public IObjectBase Caller { get; private set; }

        /// <summary>
        /// Gets or sets the verb; a string, the name by which the currently-running verb was identified.
        /// </summary>
        [CanBeNull, PublicAPI]
        public string Verb { get; set; }

        /// <summary>
        /// Gets or sets a string, everything after the first word of the command
        /// </summary>
        [CanBeNull, PublicAPI]
        public string ArgString { get; set; }

        /// <summary>
        /// Gets or sets a list of strings, the words in <see cref="ArgString"/>
        /// </summary>
        [CanBeNull, PublicAPI]
        public string[] Args { get; set; }

        /// <summary>
        /// Gets or sets a string, the direct object string found during parsing
        /// </summary>
        [CanBeNull, PublicAPI]
        public string DirectObjectString { get; set; }

        /// <summary>
        /// Gets or sets an object, the direct object value found during matching
        /// </summary>
        [CanBeNull, PublicAPI]
        public IObjectBase DirectObject { get; set; }

        /// <summary>
        /// Gets or sets a string, the prepositional phrase string found during parsing
        /// </summary>
        [CanBeNull, PublicAPI]
        public string PrepositionString { get; set; }

        /// <summary>
        /// Gets or sets a string, the indirect object string found during parsing
        /// </summary>
        [CanBeNull, PublicAPI]
        public string IndirectObjectString { get; set; }

        /// <summary>
        /// Gets or sets an object, the indirect object value found during matching
        /// </summary>
        [CanBeNull, PublicAPI]
        public IObjectBase IndirectObject { get; set; }
        
        /// <summary>
        /// Gets a text writer that can be used to send information back to the triggering object.
        /// </summary>
        [NotNull, PublicAPI]
        public TextWriter PlayerOutput { get; private set; }

        /// <summary>
        /// Gets a <see cref="StreamWriter"/> that is used by the internal connection to store input received by a player
        /// </summary>
        [NotNull]
        public PlayerInputStreamWriter PlayerInputWriterInternal { get; private set; }

        /// <summary>
        /// Gets a <see cref="StreamReader"/> that can be used by the script to retrieve text from the player waiting for it to read in
        /// </summary>
        [NotNull, PublicAPI]
        public PlayerInputStreamReader PlayerInput { get; private set; }

        /// <summary>
        /// Gets an instance of a library class that exposes functions to the script that allow it to modify objects in the database
        /// </summary>
        [NotNull, PublicAPI]
        public IDatabaseLibrary DatabaseLibrary { get; private set; }

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

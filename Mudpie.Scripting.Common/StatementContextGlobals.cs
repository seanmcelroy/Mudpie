// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StatementContextGlobals.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   Global context variables passed into a program execution context
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Scripting.Common
{
    using System;

    using JetBrains.Annotations;

    /// <summary>
    /// Global context variables passed into a program execution context
    /// </summary>
    [PublicAPI]
    public class StatementContextGlobals
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StatementContextGlobals"/> class.
        /// </summary>
        /// <param name="caller">
        /// The object on which the verb that called the currently-running
        /// verb. For the first verb called for a given command, 'caller' has the
        /// same value as <see cref="Player"/>.
        /// </param>
        public StatementContextGlobals([CanBeNull] IObjectBase caller)
        {
            if (caller == null)
            {
                throw new ArgumentNullException(nameof(caller));
            }

            this.Caller = caller;
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
        /// Gets or sets the caller, the object on which the verb that called the currently-running
        /// verb was found. For the first verb called for a given command, 'caller' has the
        /// same value as <see cref="Player"/>.
        /// </summary>
        [CanBeNull, PublicAPI]
        public IObjectBase Caller { get; protected set; }

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
    }
}

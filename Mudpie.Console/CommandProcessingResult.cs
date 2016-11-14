// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CommandProcessingResult.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A command processing result is a return type from methods that handle command input from interactive users on a connection
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console
{
    using System;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    /// <summary>
    /// A command processing result is a return type from methods that handle command input from interactive users on a connection
    /// </summary>
    internal sealed class CommandProcessingResult
    {
        public CommandProcessingResult(bool isHandled, bool isQuitting = false)
        {
            this.IsHandled = isHandled;
            this.IsQuitting = isQuitting;
        }

        public bool IsHandled { get; set; }

        public bool IsQuitting { get; set; }

        /// <summary>
        /// Gets or sets a value that, if not null, indicates the request was the 
        /// start of a message that should be read until its end,
        /// at which time this function should be invoked on the result.
        /// </summary>
        [CanBeNull]
        public Func<string, CommandProcessingResult, Task<CommandProcessingResult>> MessageHandler { get; set; }

        [CanBeNull]
        public string Message { get; set; }
    }
}

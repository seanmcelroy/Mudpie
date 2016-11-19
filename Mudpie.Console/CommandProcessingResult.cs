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
    /// <summary>
    /// A command processing result is a return type from methods that handle command input from interactive users on a connection
    /// </summary>
    internal sealed class CommandProcessingResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandProcessingResult"/> class.
        /// </summary>
        /// <param name="isHandled">Whether or not the original input was handled by the code returning this result</param>
        /// <param name="isQuitting">Whether or not the client is quitting</param>
        public CommandProcessingResult(bool isHandled, bool isQuitting = false)
        {
            this.IsHandled = isHandled;
            this.IsQuitting = isQuitting;
        }

        /// <summary>
        /// Gets a value indicating whether or not the original input was handled by the code returning this result
        /// </summary>
        public bool IsHandled { get; private set; }

        /// <summary>
        /// Gets a value indicating whether or not the client is quitting
        /// </summary>
        public bool IsQuitting { get; private set; }
    }
}

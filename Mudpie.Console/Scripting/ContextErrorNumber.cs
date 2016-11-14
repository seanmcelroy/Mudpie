// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ContextErrorNumber.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   If an execution <see cref="Context{T}" /> has ended in error, this is the general category of the error state
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Scripting
{
    /// <summary>
    /// If an execution <see cref="Context{T}"/> has ended in error, this is the general category of the error state
    /// </summary>
    public enum ContextErrorNumber
    {
        /// <summary>
        /// Authentication is required before the command can execute
        /// </summary>
        AuthenticationRequired,

        /// <summary>
        /// The requested program was not found
        /// </summary>
        ProgramNotFound,

        /// <summary>
        /// No program name or indicator was specified
        /// </summary>
        ProgramNotSpecified
    }
}

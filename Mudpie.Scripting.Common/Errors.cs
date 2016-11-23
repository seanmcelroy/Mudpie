// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Errors.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   Error return type
// </summary>
// --------------------------------------------------------------------------------------------------------------------
// ReSharper disable InconsistentNaming
namespace Mudpie.Scripting.Common
{
    /// <summary>
    /// Error return type
    /// </summary>
    public enum Errors
    {
        /// <summary>
        /// No error
        /// </summary>
        E_NONE = -100,

        /// <summary>
        /// Type mismatch
        /// </summary>
        E_TYPE = -101,

        /// <summary>
        /// Division by zero
        /// </summary>
        E_DIV = -102,

        /// <summary>
        /// Permission denied
        /// </summary>
        E_PERM = -103,

        /// <summary>
        /// Property not found
        /// </summary>
        E_PROPNF = -104,

        /// <summary>
        /// Verb not found
        /// </summary>
        E_VERBNF = -105,

        /// <summary>
        /// Variable not found
        /// </summary>
        E_VARNF = -106,

        /// <summary>
        /// Invalid indirection
        /// </summary>
        E_INVIND = -107,

        /// <summary>
        /// Recursive move
        /// </summary>
        E_RECMOVE = -108,

        /// <summary>
        /// Too many verb calls
        /// </summary>
        E_MAXREC = -109,

        /// <summary>
        /// Range error
        /// </summary>
        E_RANGE = -110,

        /// <summary>
        /// Incorrect number of arguments
        /// </summary>
        E_ARGS = -111,

        /// <summary>
        /// Move refused by destination
        /// </summary>
        E_NACC = -112,

        /// <summary>
        /// Invalid argument
        /// </summary>
        E_INVARG = -113,

        /// <summary>
        /// Resource limit exceeded
        /// </summary>
        E_QUOTA = -114,

        /// <summary>
        /// Floating-point arithmetic error
        /// </summary>
        E_FLOAT = -115
    }
}
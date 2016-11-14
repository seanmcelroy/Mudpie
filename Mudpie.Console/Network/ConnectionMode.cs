// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConnectionMode.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   The current input mode of an establish connection
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Network
{
    /// <summary>
    /// The current input mode of an establish connection
    /// </summary>
    public enum ConnectionMode
    {
        /// <summary>
        /// The current mode is unknown
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The current mode is normal, meaning, all input will be interpreted by the general matching rules
        /// </summary>
        Normal = 1,

        /// <summary>
        /// The current mode is program-based, meaning all input is being redirected to a program blocking the user's
        /// connection until it receives the information it is requesting and completes
        /// </summary>
        InteractiveProgram = 2
    }
}

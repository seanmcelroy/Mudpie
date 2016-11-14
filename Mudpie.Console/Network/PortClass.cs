// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PortClass.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   The type of port (encrypted or plain-text) of a connection
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Network
{
    /// <summary>
    /// The type of port (encrypted or plain-text) of a connection
    /// </summary>
    internal enum PortClass
    {
        /// <summary>
        /// The connection is clear text and unencrypted
        /// </summary>
        ClearText,

        /// <summary>
        /// The connection is encrypted using implicit transport layer security
        /// </summary>
        ImplicitTLS
    }
}

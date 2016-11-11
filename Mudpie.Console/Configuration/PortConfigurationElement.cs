// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PortConfigurationElement.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A configuration element that specifies a TCP port on which the process will listen for incoming requests
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Configuration
{
    using System.Configuration;

    using JetBrains.Annotations;

    /// <summary>
    /// A configuration element that specifies a TCP port on which the process will listen for incoming requests
    /// </summary>
    public class PortConfigurationElement : ConfigurationElement
    {
        /// <summary>
        /// Gets or sets the port number
        /// </summary>
        [ConfigurationProperty("number", IsRequired = true)]
        public int Port
        {
            get { return (int)(this["number"] ?? 0); }
            [UsedImplicitly]
            set { this["number"] = value; }
        }

        /// <summary>
        /// Gets or sets the protocol to listen for on the port
        /// </summary>
        [ConfigurationProperty("proto", IsRequired = true)]
        public string Protocol
        {
            get
            {
                return (string)this["proto"];
            }

            set
            {
                this["proto"] = value;
            }
        }
    }
}

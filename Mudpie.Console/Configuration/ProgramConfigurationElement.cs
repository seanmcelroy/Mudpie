// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ProgramConfigurationElement.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A configuration element that specifies where the MUD should look for programs to auto-load on startup to seed the database
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Configuration
{
    using System.Configuration;

    using JetBrains.Annotations;

    /// <summary>
    /// A configuration element that specifies where the MUD should look for programs to auto-load on startup to seed the database
    /// </summary>
    public class ProgramConfigurationElement : ConfigurationElement
    {
        /// <summary>
        /// Gets or sets the port number
        /// </summary>
        [ConfigurationProperty("directory", IsRequired = true), NotNull]
        public string Directory
        {
            get { return (string)this["directory"]; }
            [UsedImplicitly]
            set { this["directory"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the program can be triggered by a connection with no authenticated player
        /// </summary>
        [ConfigurationProperty("unauthenticated", IsRequired = false)]
        public bool Unauthenticated
        {
            get { return (bool)(this["unauthenticated"] ?? false); }
            [UsedImplicitly]
            set { this["unauthenticated"] = value; }
        }
    }
}

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MudpieConfigurationSection.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   The primary configuration section for the server instance
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Configuration
{
    using System.Configuration;
    using JetBrains.Annotations;

    /// <summary>
    /// The primary configuration section for the server instance
    /// </summary>
    [PublicAPI]
    // ReSharper disable once InconsistentNaming
    internal class MudpieConfigurationSection : ConfigurationSection
    {
        /// <summary>
        /// Gets the configuration element relating to how networking ports are made available to connect to this server instance
        /// </summary>
        [ConfigurationProperty("ports", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(PortConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        [UsedImplicitly]
        public PortConfigurationElementCollection Ports => (PortConfigurationElementCollection)base["ports"];

        /// <summary>
        /// Gets the configuration element relating to how the MUD can find programs to load into its execution space
        /// </summary>
        [ConfigurationProperty("programs")]
        [ConfigurationCollection(typeof(ProgramConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        [UsedImplicitly]
        public ProgramConfigurationElementCollection Directories => (ProgramConfigurationElementCollection)base["programs"];
    }
}

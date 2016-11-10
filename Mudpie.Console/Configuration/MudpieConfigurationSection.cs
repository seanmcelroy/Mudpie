namespace Mudpie.Console.Configuration
{
    using System.Configuration;
    using JetBrains.Annotations;

    /// <summary>
    /// The primary configuration section for the server instance
    /// </summary>
    [PublicAPI]
    // ReSharper disable once InconsistentNaming
    public class MudpieConfigurationSection : ConfigurationSection
    {
        /// <summary>
        /// Gets the configuration element relating to how networking ports are made available to connect to this server instance
        /// </summary>
        [ConfigurationProperty("ports", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(PortConfigurationElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        [UsedImplicitly]
        public PortConfigurationElementCollection Ports => (PortConfigurationElementCollection)base["ports"];
    }
}

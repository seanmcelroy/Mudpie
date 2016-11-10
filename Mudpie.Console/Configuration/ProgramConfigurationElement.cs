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
        [ConfigurationProperty("directory", IsRequired = true)]
        public string Directory
        {
            get { return (string)this["directory"]; }
            [UsedImplicitly]
            set { this["directory"] = value; }
        }
    }
}

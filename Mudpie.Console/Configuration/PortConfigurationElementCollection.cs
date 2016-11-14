// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PortConfigurationElementCollection.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A collection of <see cref="PortConfigurationElement"/> records
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Configuration
{
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.Linq;

    using JetBrains.Annotations;

    /// <summary>
    /// A collection of <see cref="PortConfigurationElement"/> records
    /// </summary>
    public class PortConfigurationElementCollection : ConfigurationElementCollection, IEnumerable<PortConfigurationElement>
    {
        /// <inheritdoc />
        public PortConfigurationElement this[int index]
        {
            get
            {
                return (PortConfigurationElement)this.BaseGet(index);
            }

            set
            {
                if (this.BaseGet(index) != null)
                    this.BaseRemove(index);
                if (value != null)
                    this.BaseAdd(index, value);
            }
        }

        /// <inheritdoc />
        public void Add([NotNull] PortConfigurationElement serviceConfig) => this.BaseAdd(serviceConfig);

        /// <inheritdoc />
        public void Clear() => this.BaseClear();

        /// <inheritdoc />
        public void Remove([NotNull] PortConfigurationElement serviceConfig) => this.BaseRemove(serviceConfig.Port.ToString(CultureInfo.InvariantCulture));

        /// <inheritdoc />
        public void RemoveAt(int index) => this.BaseRemoveAt(index);

        /// <inheritdoc />
        public void Remove([NotNull] string name) => this.BaseRemove(name);

        /// <inheritdoc />
        public new IEnumerator<PortConfigurationElement> GetEnumerator() => this.BaseGetAllKeys().Where(key => key != null).Select(key => (PortConfigurationElement)this.BaseGet(key)).GetEnumerator();

        /// <inheritdoc />
        protected override ConfigurationElement CreateNewElement() => new PortConfigurationElement();

        /// <inheritdoc />
        protected override object GetElementKey(ConfigurationElement element)
        {
            var pce = (PortConfigurationElement)element;
            return pce.Port.ToString(CultureInfo.InvariantCulture);
        }
    }
}

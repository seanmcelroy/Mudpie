// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PortConfigurationElementCollection.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   Defines the PortConfigurationElementCollection type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Configuration
{
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.Linq;

    using JetBrains.Annotations;

    public class PortConfigurationElementCollection : ConfigurationElementCollection, IEnumerable<PortConfigurationElement>
    {
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

        public void Add([NotNull] PortConfigurationElement serviceConfig)
        {
            this.BaseAdd(serviceConfig);
        }

        public void Clear()
        {
            this.BaseClear();
        }

        public void Remove([NotNull] PortConfigurationElement serviceConfig)
        {
            this.BaseRemove(serviceConfig.Port.ToString(CultureInfo.InvariantCulture));
        }

        public void RemoveAt(int index)
        {
            this.BaseRemoveAt(index);
        }

        public void Remove([NotNull] string name)
        {
            this.BaseRemove(name);
        }

        public new IEnumerator<PortConfigurationElement> GetEnumerator()
        {
            return this.BaseGetAllKeys().Select(key => (PortConfigurationElement)this.BaseGet(key)).GetEnumerator();
        }

        /// <summary>When overridden in a derived class, creates a new <see cref="T:System.Configuration.ConfigurationElement" />.</summary>
        /// <returns>A newly created <see cref="T:System.Configuration.ConfigurationElement" />.</returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new PortConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var pce = (PortConfigurationElement)element;
            return pce.Port.ToString(CultureInfo.InvariantCulture);
        }
    }
}

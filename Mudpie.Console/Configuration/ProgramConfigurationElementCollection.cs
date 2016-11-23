// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ProgramConfigurationElementCollection.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A collection of <see cref="ProgramConfigurationElement" /> records
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
    /// A collection of <see cref="ProgramConfigurationElement"/> records
    /// </summary>
    public class ProgramConfigurationElementCollection : ConfigurationElementCollection, IEnumerable<ProgramConfigurationElement>
    {
        /// <inheritdoc />
        [NotNull]
        [UsedImplicitly]
        public ProgramConfigurationElement this[int index]
        {
            get
            {
                return (ProgramConfigurationElement)this.BaseGet(index);
            }

            set
            {
                if (this.BaseGet(index) != null)
                {
                    this.BaseRemove(index);
                }

                this.BaseAdd(index, value);
            }
        }

        /// <inheritdoc />
        [UsedImplicitly]
        public void Add([NotNull] ProgramConfigurationElement serviceConfig) => this.BaseAdd(serviceConfig);

        /// <inheritdoc />
        [UsedImplicitly]
        public void Clear() => this.BaseClear();

        /// <inheritdoc />
        [UsedImplicitly]
        public void Remove([NotNull] ProgramConfigurationElement serviceConfig) => this.BaseRemove(serviceConfig.Directory);

        /// <inheritdoc />
        [UsedImplicitly]
        public void RemoveAt(int index) => this.BaseRemoveAt(index);

        /// <inheritdoc />
        [UsedImplicitly]
        public void Remove([NotNull] string name) => this.BaseRemove(name);

        /// <inheritdoc />
        public new IEnumerator<ProgramConfigurationElement> GetEnumerator() => this.BaseGetAllKeys().Where(key => key != null).Select(key => (ProgramConfigurationElement)this.BaseGet(key)).GetEnumerator();

        /// <inheritdoc />
        protected override ConfigurationElement CreateNewElement()
        {
            return new ProgramConfigurationElement();
        }

        /// <inheritdoc />
        protected override object GetElementKey(ConfigurationElement element)
        {
            var pce = (ProgramConfigurationElement)element;
            return pce.Directory.ToString(CultureInfo.InvariantCulture);
        }
    }
}

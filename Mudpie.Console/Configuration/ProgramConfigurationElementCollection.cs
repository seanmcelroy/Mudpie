namespace Mudpie.Console.Configuration
{
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.Linq;

    using JetBrains.Annotations;

    public class ProgramConfigurationElementCollection : ConfigurationElementCollection, IEnumerable<ProgramConfigurationElement>
    {
        /// <summary>When overridden in a derived class, creates a new <see cref="T:System.Configuration.ConfigurationElement" />.</summary>
        /// <returns>A newly created <see cref="T:System.Configuration.ConfigurationElement" />.</returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new ProgramConfigurationElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var pce = (ProgramConfigurationElement)element;
            return pce.Directory.ToString(CultureInfo.InvariantCulture);
        }

        public ProgramConfigurationElement this[int index]
        {
            get { return (ProgramConfigurationElement)this.BaseGet(index); }
            set
            {
                if (this.BaseGet(index) != null)
                    this.BaseRemove(index);
                this.BaseAdd(index, value);
            }
        }

        public void Add(ProgramConfigurationElement serviceConfig)
        {
            this.BaseAdd(serviceConfig);
        }

        public void Clear()
        {
            this.BaseClear();
        }

        public void Remove([NotNull] ProgramConfigurationElement serviceConfig)
        {
            this.BaseRemove(serviceConfig.Directory);
        }

        public void RemoveAt(int index)
        {
            this.BaseRemoveAt(index);
        }

        public void Remove(string name)
        {
            this.BaseRemove(name);
        }

        public new IEnumerator<ProgramConfigurationElement> GetEnumerator()
        {
            return this.BaseGetAllKeys().Select(key => (ProgramConfigurationElement)this.BaseGet(key)).GetEnumerator();
        }
    }
}

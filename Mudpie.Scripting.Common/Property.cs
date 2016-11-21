// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Property.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   An attribute on a <see cref="ObjectBase" />
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Scripting.Common
{
    using JetBrains.Annotations;
    using Newtonsoft.Json;

    /// <summary>
    /// An attribute on an object
    /// </summary>
    public class Property
    {
        /// <summary>
        /// Gets or sets the name of the property
        /// </summary>
        [NotNull]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the owner o
        /// </summary>
        public DbRef Owner { get; set; }

        /// <summary>
        /// Gets or sets the value of the property
        /// </summary>
        [NotNull]
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether anyone can read the value of this property
        /// </summary>
        public bool PublicReadable { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether anyone can write the value of this property
        /// </summary>
        public bool PublicWriteable { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether anyone can write the value of this property
        /// </summary>
        public bool ChangeOwnershipInDescendents { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether 
        /// </summary>
        [JsonIgnore]
        internal bool Inherited { get; set; }
    }
}

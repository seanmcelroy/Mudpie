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
    using System;

    using JetBrains.Annotations;
    using Newtonsoft.Json;

    /// <summary>
    /// An attribute on an object
    /// </summary>
    public class Property
    {
        /// <summary>
        /// The name of the 'description' property
        /// </summary>
        public const string DESCRIPTION = "_/de";

        /// <summary>
        /// Initializes a new instance of the <see cref="Property"/> class.
        /// </summary>
        // ReSharper disable once NotNullMemberIsNotInitialized
        public Property()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Property"/> class.
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <param name="value">The value of the property</param>
        /// <param name="owner">The owner of the property</param>
        public Property([NotNull] string name, [NotNull] object value, DbRef owner)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (owner <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), owner, $"Owner must be set; value provided was {owner}");
            }

            this.Name = name;
            this.Value = value;
            this.Owner = owner;
        }

        /// <summary>
        /// Gets or sets the name of the property
        /// </summary>
        [NotNull]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the owner of the property
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

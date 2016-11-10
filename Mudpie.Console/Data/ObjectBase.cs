// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ObjectBase.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   The base definition of any object in the MUD
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Data
{
    using System;

    using JetBrains.Annotations;

    /// <summary>
    /// The base definition of any object in the MUD
    /// </summary>
    public abstract class ObjectBase
    {
        /// <summary>
        /// Gets or sets the globally unique identifier of the object
        /// </summary>
        [NotNull]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the name of the object
        /// </summary>
        [NotNull]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the object if a user were to observe it directly
        /// </summary>
        [CanBeNull]
        public string Description { get; set; }

        protected ObjectBase()
        {
        }

        protected ObjectBase([NotNull] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            this.Name = name;
        }
    }
}

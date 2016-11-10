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
    [PublicAPI]
    public abstract class ObjectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectBase"/> class.
        /// </summary>
        protected ObjectBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectBase"/> class.
        /// </summary>
        /// <param name="name">Name of the object</param>
        /// <exception cref="ArgumentNullException">Thrown if the name is null</exception>
        protected ObjectBase([NotNull] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            this.Name = name;
        }

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
    }
}

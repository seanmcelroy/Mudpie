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

    using Mudpie.Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

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
        /// Gets or sets the database reference of the object
        /// </summary>
        public DbRef DbRef { get; set; } = -1;

        /// <summary>
        /// Gets or sets the globally unique identifier of the object
        /// </summary>
        /// <remarks>
        /// This exists separate from a DBREF to allow for synchronization scenarios in the future
        /// </remarks>
        [NotNull]
        public string InternalId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the name of the object
        /// </summary>
        [NotNull]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the aliases of this object
        /// </summary>
        [CanBeNull]
        public string[] Aliases { get; set; }

        /// <summary>
        /// Gets or sets the description of the object if a user were to observe it directly
        /// </summary>
        [CanBeNull]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the location of this object
        /// </summary>
        public DbRef Location { get; set; } = DbRef.NOTHING;

        /// <summary>
        /// Gets or sets the parent of this object, from which it inherits properties and verbs
        /// </summary>
        public DbRef Parent { get; set; } = DbRef.NOTHING;

        [NotNull]
        public static T Create<T>([NotNull] ICacheClient redis) where T : ObjectBase, new()
        {
            var newObject = new T
                                {
                                    DbRef = Convert.ToInt32(redis.Database.StringIncrement("mudpie::dbref:counter"))
                                };

            return newObject;
        }
    }
}

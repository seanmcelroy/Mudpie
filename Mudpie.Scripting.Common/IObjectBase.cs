// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IObjectBase.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A basic interface for any 'object' in the underlying data store
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Scripting.Common
{
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A basic interface for any 'object' in the underlying data store
    /// </summary>
    [PublicAPI]
    public interface IObjectBase
    {
        /// <summary>
        /// Gets or sets the database reference of the object
        /// </summary>
        DbRef DbRef { get; set; }

        /// <summary>
        /// Gets or sets the name of the object
        /// </summary>
        [NotNull]
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the aliases of this object
        /// </summary>
        [CanBeNull]
        string[] Aliases { get; set; }

        /// <summary>
        /// Gets or sets the location of this object
        /// </summary>
        DbRef Location { get; set; }

        /// <summary>
        /// Gets or sets the inverse of 'location', the contents of this object
        /// </summary>
        [CanBeNull]
        DbRef[] Contents { get; set; }

        /// <summary>
        /// Gets or sets the parent of this object, from which it inherits properties and verbs
        /// </summary>
        DbRef Parent { get; set; }

        /// <summary>
        /// Gets or sets the properties on the object
        /// </summary>
        [CanBeNull]
        Property[] Properties { get; set; }

        /// <summary>
        /// Changes the location of this object to a new place
        /// </summary>
        /// <param name="newLocation">The reference of the new <see cref="IObjectBase"/> into which this object should be placed</param>
        /// <param name="redis">The client proxy to access the underlying data store</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>A task object used to await this method for completion</returns>
        [NotNull]
        Task MoveAsync(DbRef newLocation, [NotNull] ICacheClient redis, CancellationToken cancellationToken);

        /// <summary>
        /// Changes the parent of this object to a new parent, for inheriting verbs, properties, etc.
        /// </summary>
        /// <param name="newParent">The reference of the new <see cref="IObjectBase"/> from which this object should descend and inherit behaviors</param>
        /// <param name="redis">The client proxy to access the underlying data store</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>A task object used to await this method for completion</returns>
        [NotNull]
        Task ReparentAsync(DbRef newParent, [NotNull] ICacheClient redis, CancellationToken cancellationToken);

        /// <summary>
        /// Saves the object back to the persistent data store
        /// </summary>
        /// <param name="redis">The client proxy to access the underlying data store</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>A task object used to await this method for completion</returns>
        [NotNull]
        Task SaveAsync([NotNull] ICacheClient redis, CancellationToken cancellationToken);
    }
}

namespace Mudpie.Scripting.Common
{
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using StackExchange.Redis.Extensions.Core;

    public interface IObjectBase
    {
        /// <summary>
        /// Gets or sets the database reference of the object
        /// </summary>
        DbRef DbRef { get; set; }

        /// <summary>
        /// Gets or sets the name of the object
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets or sets the aliases of this object
        /// </summary>
        string[] Aliases { get; set; }

        /// <summary>
        /// Gets or sets the description of the object if a user were to observe it directly
        /// </summary>
        string Description { get; set; }

        /// <summary>
        /// Gets or sets the location of this object
        /// </summary>
        DbRef Location { get; set; }

        /// <summary>
        /// Gets or sets the inverse of 'location', the contents of this object
        /// </summary>
        DbRef[] Contents { get; set; }

        /// <summary>
        /// Gets or sets the parent of this object, from which it inherits properties and verbs
        /// </summary>
        DbRef Parent { get; set; }

        Task MoveAsync(DbRef newLocation, [NotNull] ICacheClient redis);

        Task ReparentAsync(DbRef newParent, [NotNull] ICacheClient redis);

        /// <summary>
        /// Saves the object back to the persistent data store
        /// </summary>
        /// <param name="redis">The client proxy to access the underlying data store</param>
        /// <returns>A task object used to await this method for completion</returns>
        [NotNull]
        Task SaveAsync([NotNull] ICacheClient redis);
    }


}

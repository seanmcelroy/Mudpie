// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IDatabaseLibrary.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A set of routines that allow a script to modify objects in the underlying data store
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Scripting.Common
{
    using JetBrains.Annotations;

    /// <summary>
    /// A set of routines that allow a script to modify objects in the underlying data store
    /// </summary>
    [PublicAPI]
    public interface IDatabaseLibrary
    {
        /// <summary>
        /// Creates a new room in the database and returns its <see cref="DbRef"/>
        /// </summary>
        /// <param name="name">The name of the new room</param>
        /// <returns>The database reference for the newly created object.
        /// If the operation failed, this will return <see cref="DbRef.Nothing"/></returns>
        DbRef CreateRoom([NotNull] string name);

        /// <summary>
        /// Renames an object
        /// </summary>
        /// <param name="reference">The <see cref="DbRef"/> of the object to rename</param>
        /// <param name="newName">The new name for the object</param>
        /// <returns>A value indicating whether the rename operation was successful</returns>
        bool Rename(DbRef reference, [NotNull]string newName);
    }
}

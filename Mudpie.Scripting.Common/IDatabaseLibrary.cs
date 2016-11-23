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
        bool Rename(DbRef reference, [NotNull] string newName);

        /// <summary>
        /// Retrieves the value of the property with the given name
        /// </summary>
        /// <param name="reference">The <see cref="DbRef"/> of the object from which to retrieve a property</param>
        /// <param name="name">The name of the property to retrieve</param>
        /// <returns>The value of the property if it exists; otherwise, null</returns>
        [CanBeNull, Pure]
        object GetProperty(DbRef reference, [NotNull] string name);

        /// <summary>
        /// Sets the value of a property on an object
        /// </summary>
        /// <param name="reference">The <see cref="DbRef"/> of the object on which to set a property</param>
        /// <param name="propertyName">The canonical path and name of the property to update</param>
        /// <param name="propertyValue">The new value for the property.  If null or <see cref="DbRef.Nothing"/>, the property will be unset.</param>
        /// <returns>A value indicating whether or not the property was changed as requested</returns>
        bool SetProperty(DbRef reference, [NotNull] string propertyName, [CanBeNull] object propertyValue);
    }
}

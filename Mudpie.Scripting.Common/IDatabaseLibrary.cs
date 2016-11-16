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
    /// <summary>
    /// A set of routines that allow a script to modify objects in the underlying data store
    /// </summary>
    public interface IDatabaseLibrary
    {
        /// <summary>
        /// Renames an object
        /// </summary>
        /// <param name="reference">The <see cref="DbRef"/> of the object to rename</param>
        /// <param name="newName">The new name for the object</param>
        /// <returns>A value indicating whether the rename operation was successful</returns>
        bool Rename(DbRef reference, string newName);
    }
}

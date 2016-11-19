// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IComposedObject.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A composed object is a materialized view of a <see cref="ObjectBase" /> that has any inheritance <see cref="DbRef" />'s retrieved from the <see cref="CacheManager" />
//   to provide easily accessible inherited object graph access
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Mudpie.Server.Data
{
    using System.Collections.ObjectModel;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;

    /// <summary>
    /// A composed object is a materialized view of a <see cref="ObjectBase"/> that has any inheritance <see cref="DbRef"/>'s retrieved from the <see cref="CacheManager"/>
    /// to provide easily accessible inherited object graph access
    /// </summary>
    /// <remarks>
    /// This type exists to allow for strong typing of the DataObject member of implementations</remarks>
    public interface IComposedObject
    {
        /// <summary>
        /// Gets or sets the <see cref="ObjectBase"/>s representing the contents <see cref="DbRef"/> of the DataObject
        /// </summary>
        [CanBeNull]
        ReadOnlyCollection<IComposedObject<ObjectBase>> Contents { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ObjectBase"/> representing the location <see cref="DbRef"/> of the DataObject
        /// </summary>
        [CanBeNull]
        IComposedObject Location { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ObjectBase"/> representing the parent <see cref="DbRef"/> of the DataObject
        /// </summary>
        [CanBeNull]
        IComposedObject Parent { get; set; }
    }
}
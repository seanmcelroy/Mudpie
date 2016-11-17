// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IComposedObjectT.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A composed object is a materialized view of a <see cref="ObjectBase" /> that has any inheritance <see cref="DbRef" />'s retrieved from the <see cref="CacheManager" />
//   to provide easily accessible inherited object graph access
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Server.Data
{
    using Mudpie.Scripting.Common;

    /// <summary>
    /// A composed object is a materialized view of a <see cref="ObjectBase"/> that has any inheritance <see cref="DbRef"/>'s retrieved from the <see cref="CacheManager"/>
    /// to provide easily accessible inherited object graph access
    /// </summary>
    /// <remarks>
    /// This type exists to allow for strong typing of the <see cref="DataObject"/> member of implementations</remarks>
    public interface IComposedObject<out T> : IComposedObject where T : ObjectBase
    {
        /// <summary>
        /// Gets the underlying <see cref="ObjectBase"/> that was collapsed to compose this object-oriented version that inherits parent properties
        /// </summary>
        new T DataObject { get; }
    }
}
// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ComposedObject.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A composed object is a materialized view of a <see cref="ObjectBase" /> that has any inheritance <see cref="DbRef" />'s retrieved from the <see cref="CacheManager" />
//   to provide easily accessible inherited object graph access
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Server.Data
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;

    using Newtonsoft.Json;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A composed object is a materialized view of a <see cref="ObjectBase"/> that has any inheritance <see cref="DbRef"/>'s retrieved from the <see cref="CacheManager"/>
    /// to provide easily accessible inherited object graph access
    /// </summary>
    public sealed class ComposedObject
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="ComposedObject"/> class from being created. 
        /// </summary>
        /// <param name="dataObject">The underlying <see cref="ObjectBase"/> that was collapsed to compose this object-oriented version that inherits parent properties</param>
        private ComposedObject([NotNull] ObjectBase dataObject)
        {
            this.DataObject = dataObject;
        }

        /// <summary>
        /// Creates a new composed object
        /// </summary>
        /// <param name="redis">The client proxy to access the underlying data store to hydrate this instance</param>
        /// <param name="dataObject">The underlying <see cref="ObjectBase"/> that was collapsed to compose this object-oriented version that inherits parent properties</param>
        /// <returns>A tuple indicating whether the composed happened flawlessly (no unresolved references, and thus, cacheable), and the object as composed as it could be</returns>
        public static async Task<Tuple<bool, ComposedObject>> CreateAsync([NotNull] ICacheClient redis, [NotNull] ObjectBase dataObject)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            if (dataObject == null)
            {
                throw new ArgumentNullException(nameof(dataObject));
            }

            var ret = new ComposedObject(dataObject);
            var perfect = true;

            var taskLocation = Task.Run(async () =>
            {
                var composedLocation = await CacheManager.LookupOrRetrieveAsync(dataObject.Location, redis, async dbref => await Room.GetAsync(redis, dataObject.Location));
                ret.Location = composedLocation;
                perfect = perfect && (dataObject.Location <= 0 || composedLocation != null);
            });

            if (dataObject.Contents != null)
            {
                var contents = new List<ComposedObject>();
                Parallel.ForEach(
                    dataObject.Contents,
                    async dbref =>
                    {
                        var composedContent = await CacheManager.LookupOrRetrieveAsync(dbref, redis, async d => await ObjectBase.GetAsync(redis, d));
                        if (composedContent != null)
                        {
                            contents.Add(composedContent);
                        }

                        perfect = perfect && (dbref <= 0 || composedContent != null);
                    });

                ret.Contents = contents.AsReadOnly();
            }

            var taskParent = Task.Run(async () =>
            {
                var composedParent = await CacheManager.LookupOrRetrieveAsync(dataObject.Parent, redis, async dbref => await ObjectBase.GetAsync(redis, dataObject.Parent));
                ret.Parent = composedParent;
                perfect = perfect && (dataObject.Parent <= 0 || composedParent != null);
            });

            await Task.WhenAll(taskLocation, taskParent);

            return new Tuple<bool, ComposedObject>(perfect, ret);
        }

        /// <summary>
        /// Gets the underlying <see cref="ObjectBase"/> that was collapsed to compose this object-oriented version that inherits parent properties
        /// </summary>
        [NotNull]
        [JsonIgnore]
        public ObjectBase DataObject { get; private set; }

        /// <summary>
        /// Gets the <see cref="ObjectBase"/> representing the location <see cref="DbRef"/> of the <see cref="DataObject"/>
        /// </summary>
        [CanBeNull]
        [JsonIgnore]
        public ComposedObject Location { get; private set; }

        /// <summary>
        /// Gets the <see cref="ObjectBase"/>s representing the contents <see cref="DbRef"/> of the <see cref="DataObject"/>
        /// </summary>
        [CanBeNull]
        [JsonIgnore]
        public ReadOnlyCollection<ComposedObject> Contents { get; private set; }

        /// <summary>
        /// Gets the <see cref="ObjectBase"/> representing the parent <see cref="DbRef"/> of the <see cref="DataObject"/>
        /// </summary>
        [CanBeNull]
        [JsonIgnore]
        public ComposedObject Parent { get; private set; }
    }
}

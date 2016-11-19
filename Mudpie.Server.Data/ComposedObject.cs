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
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;

    using Newtonsoft.Json;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A composed object is a materialized view of a <see cref="ObjectBase"/> that has any inheritance <see cref="DbRef"/>'s retrieved from the <see cref="CacheManager"/>
    /// to provide easily accessible inherited object graph access
    /// </summary>
    /// <typeparam name="T">The type of the object this composed object adapts</typeparam>
    public class ComposedObject<T> : IComposedObject<T>
        where T : ObjectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ComposedObject{T}"/> class.
        /// </summary>
        /// <param name="dataObject">
        /// The underlying <see cref="ObjectBase"/> that was collapsed to compose this object-oriented version that inherits parent properties
        /// </param>
        private ComposedObject([NotNull] T dataObject)
        {
            this.DataObject = dataObject;
        }
        
        /// <inheritdoc />
        [NotNull]
        [JsonIgnore]
        public T DataObject { get; }

        /// <inheritdoc />
        [JsonIgnore]
        public IComposedObject Location { get; set; }

        /// <inheritdoc />
        [JsonIgnore]
        public ReadOnlyCollection<IComposedObject<ObjectBase>> Contents { get; set; }

        /// <inheritdoc />
        [JsonIgnore]
        public IComposedObject Parent { get; set; }

        /// <summary>
        /// Creates a new composed object
        /// </summary>
        /// <param name="redis">The client proxy to access the underlying data store to hydrate this instance</param>
        /// <param name="dataObject">The underlying <see cref="ObjectBase"/> that was collapsed to compose this object-oriented version that inherits parent properties</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>A tuple indicating whether the composed happened flawlessly (no unresolved references, and thus, cacheable), and the object as composed as it could be</returns>
        [NotNull, ItemNotNull]
        public static async Task<Tuple<bool, IComposedObject<T>>> CreateAsync([NotNull] ICacheClient redis, [NotNull] T dataObject, CancellationToken cancellationToken)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            if (dataObject == null)
            {
                throw new ArgumentNullException(nameof(dataObject));
            }

            var ret = new ComposedObject<T>(dataObject);
            var perfect = true;

            var taskLocation = Task.Run(
                async () =>
                {
                    var composedLocation = await CacheManager.LookupOrRetrieveAsync<ObjectBase>(dataObject.Location, redis, async (d, token) => await Room.GetAsync(redis, dataObject.Location, token), cancellationToken);
                    ret.Location = composedLocation;
                    perfect = perfect && (dataObject.Location <= 0 || composedLocation != null);
                },
                cancellationToken);

            if (dataObject.Contents != null)
            {
                var contents = new List<IComposedObject<ObjectBase>>();
                Parallel.ForEach(
                    dataObject.Contents,
                    async dbref =>
                    {
                        var composedContent = await CacheManager.LookupOrRetrieveAsync(dbref, redis, async (d, token) => await ObjectBase.GetAsync(redis, d, token), cancellationToken);
                        if (composedContent != null)
                        {
                            contents.Add(composedContent);
                        }

                        perfect = perfect && (dbref <= 0 || composedContent != null);
                    });

                ret.Contents = contents.AsReadOnly();
            }

            var taskParent = Task.Run(
                async () =>
                {
                    var composedParent = await CacheManager.LookupOrRetrieveAsync(dataObject.Parent, redis, async (d, token) => await ObjectBase.GetAsync(redis, dataObject.Parent, token), cancellationToken);
                    ret.Parent = composedParent;
                    perfect = perfect && (dataObject.Parent <= 0 || composedParent != null);
                }, 
                cancellationToken);

            await Task.WhenAll(taskLocation, taskParent);

            return new Tuple<bool, IComposedObject<T>>(perfect, ret);
        }
    }
}

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CacheManager.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A manager class for handling the temporary
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Server.Data
{
    using System;
    using System.Runtime.Caching;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A manager class for handling the temporary 
    /// </summary>
    public static class CacheManager
    {
        /// <summary>
        /// The internal last recently used cache
        /// </summary>
        [NotNull]
        private static readonly MemoryCache Cache = MemoryCache.Default;

        /// <summary>
        /// The default sliding expiration policy for cached <see cref="ObjectBase"/> items, which is 10 minutes
        /// </summary>
        [NotNull]
        private static readonly CacheItemPolicy Policy = new CacheItemPolicy
                                                             {
                                                                 SlidingExpiration = new TimeSpan(0, 0, 10, 0, 0)
                                                             };

        /// <summary>
        /// Looks up a <see cref="ComposedObject{T}"/> in the cache, if it is cached.  If it is not cached,
        /// it will be retrieved from the underlying data store, composed, cached, and returned.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="ObjectBase"/> to retrieve</typeparam>
        /// <param name="reference">The <see cref="DbRef"/> of the object to retrieve</param>
        /// <param name="redis">The client to access the data store to compose the object, if necessary</param>
        /// <param name="retrieveFunction">The function to retrieve the object with the supplied <paramref name="reference"/></param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>The composed representation of the object with the supplied <see cref="reference"/></returns>
        [NotNull, Pure, ItemCanBeNull]
        public static async Task<IComposedObject<T>> LookupOrRetrieveAsync<T>(
            DbRef reference,
            [NotNull] ICacheClient redis,
            [NotNull] Func<DbRef, CancellationToken, Task<T>> retrieveFunction,
            CancellationToken cancellationToken) where T : ObjectBase
        {
            if (reference.Equals(DbRef.Ambiguous) || reference.Equals(DbRef.FailedMatch)
                || reference.Equals(DbRef.Nothing))
            {
                return null;
            }

            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            if (retrieveFunction == null)
            {
                throw new ArgumentNullException(nameof(retrieveFunction));
            }

            if (reference.Equals(DbRef.Nothing))
            {
                return null;
            }

            if (Cache.Contains(reference))
            {
                return (IComposedObject<T>)Cache.Get(reference);
            }

            var obj = await retrieveFunction.Invoke(reference, cancellationToken);
            if (obj == null)
            {
                return null;
            }

            var composition = await ComposedObject<T>.CreateAsync(redis, (T)obj, cancellationToken);
            if (composition.Item1)
            {
                // The composition was perfect!  No unresolved references, so cache it as-is.
                Cache.Add(reference, composition.Item2, Policy);
            }

            return composition.Item2;
        }

        /// <summary>
        /// Updates a cached copy of a <see cref="ComposedObject{T}"/>
        /// with an updated copy of the underlying <see cref="ObjectBase"/>
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="ObjectBase"/> to retrieve</typeparam>
        /// <param name="reference">The <see cref="DbRef"/> of the object to retrieve</param>
        /// <param name="redis">The client to access the data store to compose the object</param>
        /// <param name="updatedDataObject">The updated <see cref="ObjectBase"/></param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>The composed representation of the object with the supplied <see cref="reference"/></returns>
        [NotNull, ItemNotNull]
        public static async Task<IComposedObject<T>> UpdateAsync<T>(
            DbRef reference,
            [NotNull] ICacheClient redis,
            [NotNull] T updatedDataObject,
            CancellationToken cancellationToken)
            where T : ObjectBase
        {
            if (Cache.Contains(reference))
            {
                Cache.Remove(reference);
            }

            var composition = await ComposedObject<T>.CreateAsync(redis, updatedDataObject, cancellationToken);
            if (composition.Item1)
            {
                // The composition was perfect!  No unresolved references, so cache it as-is.
                Cache.Add(reference, composition.Item2, Policy);
            }

            return composition.Item2;
        }
    }
}

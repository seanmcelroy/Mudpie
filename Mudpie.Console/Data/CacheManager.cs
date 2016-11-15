namespace Mudpie.Console.Data
{
    using System;
    using System.Diagnostics;
    using System.Runtime.Caching;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

    public static class CacheManager
    {
        private static readonly MemoryCache Cache = MemoryCache.Default;

        private static readonly CacheItemPolicy Policy = new CacheItemPolicy
        {
            SlidingExpiration = new TimeSpan(0, 0, 10, 0, 0)
        };

        /// <summary>
        /// Looks up a <see cref="ComposedObject"/> in the cache, if it is cached.  If it is not cached,
        /// it willl be retrieved from the underlying data store, composed, cached, and returned.
        /// </summary>
        /// <typeparam name="T">The type of the <see cref="ObjectBase"/> to retrieve</typeparam>
        /// <param name="reference">The <see cref="DbRef"/> of the object to retrieve</param>
        /// <param name="redis">The client to access the data store to compose the object, if necessary</param>
        /// <param name="retrieveFunction">The function to retrieve the object with the supplied <paramref name="reference"/></param>
        /// <returns>The composed representation of the object with the supplied <see cref="reference"/></returns>
        [NotNull, Pure, ItemCanBeNull]
        public static async Task<ComposedObject> LookupOrRetrieveAsync<T>(DbRef reference, [NotNull] ICacheClient redis, [NotNull] Func<DbRef, Task<T>> retrieveFunction)
            where T : ObjectBase
        {
            if (redis == null)
                throw new ArgumentNullException(nameof(redis));
            if (retrieveFunction == null)
                throw new ArgumentNullException(nameof(retrieveFunction));
            if (reference.Equals(DbRef.NOTHING))
                return null;
            if (Cache.Contains(reference))
                return (ComposedObject)Cache.Get(reference);

            var obj = await retrieveFunction.Invoke(reference);
            Debug.Assert(obj != null, "obj != null");

            var composedObject = new ComposedObject(redis, obj);
            Cache.Add(reference, composedObject, Policy);

            return composedObject;
        }
    }
}

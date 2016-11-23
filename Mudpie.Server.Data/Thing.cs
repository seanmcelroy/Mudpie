// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Thing.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A thing is a generic object that has behaviors or verbs attached
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Server.Data
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A thing is a generic object that has behaviors or verbs attached
    /// </summary>
    public class Thing : ObjectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Thing"/> class.
        /// </summary>
        /// <param name="name">The name of the thing</param>
        /// <param name="owner">The reference of the owner of the object</param>
        public Thing([NotNull] string name, DbRef owner)
            : base(name, owner)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (owner <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), owner, $"Owner must be set; value provided was {owner}");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Thing"/> class.
        /// </summary>
        [Obsolete("Only made public for a generic type parameter requirement", false)]

        // ReSharper disable once NotNullMemberIsNotInitialized
        public Thing()
        {
        }

        /// <summary>
        /// Creates a new thing with the specified name
        /// </summary>
        /// <param name="redis">The client proxy to access the underlying data store</param>
        /// <param name="name">The name of the new thing</param>
        /// <returns>The newly-created <see cref="Thing"/> object</returns>
        [NotNull, ItemNotNull]
        public static async Task<Thing> CreateAsync([NotNull] ICacheClient redis, [NotNull] string name)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            var newThing = await CreateAsync<Thing>(redis);
            newThing.Name = name;
            return newThing;
        }

        /// <summary>
        /// Loads a <see cref="Thing"/> from the cache or the data store
        /// </summary>
        /// <param name="redis">The client proxy to the underlying data store</param>
        /// <param name="playerRef">The <see cref="DbRef"/> of the <see cref="Thing"/> to load</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>The <see cref="Player"/> if found; otherwise, null</returns>
        [NotNull, Pure, ItemCanBeNull]
        public static new async Task<Thing> GetAsync([NotNull] ICacheClient redis, DbRef playerRef, CancellationToken cancellationToken) => (await CacheManager.LookupOrRetrieveAsync(playerRef, redis, async (d, token) => await redis.GetAsync<Thing>($"mudpie::thing:{d}"), cancellationToken))?.DataObject;

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            // If parameter is null return false.
            // ReSharper disable once UseNullPropagation
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to Player return false.
            var p = obj as Thing;

            // ReSharper disable once RedundantCast
            if ((object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return this.DbRef.Equals(p.DbRef);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.DbRef.ToString().GetHashCode();
        }

        /// <inheritdoc />
        public override async Task SaveAsync(ICacheClient redis, CancellationToken cancellationToken)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            // ReSharper disable PossibleNullReferenceException
            await Task.WhenAll(
                    redis.SetAddAsync<string>("mudpie::things", this.DbRef),
                    redis.AddAsync($"mudpie::thing:{this.DbRef}", this),
                    CacheManager.UpdateAsync(this.DbRef, redis, this, cancellationToken));
            // ReSharper restore PossibleNullReferenceException
        }
    }
}

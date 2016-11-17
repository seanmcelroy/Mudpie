// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Room.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A room is a place where objects can be located
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Server.Data
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A room is a place where objects can be located
    /// </summary>
    public class Room : ObjectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Room"/> class.
        /// </summary>
        /// <param name="roomName">The name of the room</param>
        /// <param name="owner">The reference of the owner of the object</param>
        public Room([NotNull] string roomName, DbRef owner)
            : base(roomName, owner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Room"/> class.
        /// </summary>
        [Obsolete("Only made public for a generic type parameter requirement", false)]
        public Room()
        {
        }

        /// <summary>
        /// Gets a <see cref="Room"/> from the underlying data store
        /// </summary>
        /// <param name="redis">The client proxy to access the underlying data store</param>
        /// <param name="roomRef">The <see cref="DbRef"/> of the link to retrieve from the data store</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>The matching <see cref="Room"/>, if it exists for the supplied <paramref name="roomRef"/>; otherwise, null.</returns>
        [NotNull, Pure, ItemCanBeNull]
        public static new async Task<Room> GetAsync([NotNull] ICacheClient redis, DbRef roomRef, CancellationToken cancellationToken) => (await CacheManager.LookupOrRetrieveAsync(roomRef, redis, async (d, token) => await redis.GetAsync<Room>($"mudpie::room:{d}"), cancellationToken))?.DataObject;

        /// <inheritdoc />
        public override async Task SaveAsync(ICacheClient redis, CancellationToken cancellationToken)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            await
                Task.WhenAll(
                    redis.SetAddAsync<string>("mudpie::rooms", this.DbRef),
                    redis.AddAsync($"mudpie::room:{this.DbRef}", this),
                    CacheManager.UpdateAsync(this.DbRef, redis, this, cancellationToken));
        }
    }
}

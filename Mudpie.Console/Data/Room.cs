// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Room.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A room is a place where objects can be located
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Data
{
    using System;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A room is a place where objects can be located
    /// </summary>
    public class Room : ObjectBase
    {
        [NotNull, Pure, ItemCanBeNull]
        public static new async Task<Room> GetAsync([NotNull] ICacheClient redis, DbRef roomRef) => await redis.GetAsync<Room>($"mudpie::room:{roomRef}");

        /// <inheritdoc />
        public override async Task SaveAsync(ICacheClient redis)
        {
            if (redis == null)
                throw new ArgumentNullException(nameof(redis));

            await redis.SetAddAsync<string>("mudpie::rooms", this.DbRef);
            await redis.AddAsync($"mudpie::room:{this.DbRef}", this);
        }
    }
}

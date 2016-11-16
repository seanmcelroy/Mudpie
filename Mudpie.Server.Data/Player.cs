// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Player.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A player is a general thing that represents the character owned and controlled by a real user
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Server.Data
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A player is a general thing that represents the character owned and controlled by a real user
    /// </summary>
    public class Player : ObjectBase
    {
        /// <summary>
        /// Gets or sets the identity of the user for authentication
        /// </summary>
        [NotNull]
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the hash of the player's password
        /// </summary>
        [CanBeNull]
        public string PasswordHash { get; set; }

        /// <summary>
        /// Gets or sets the salt used to hash the player's password
        /// </summary>
        [CanBeNull]
        public string PasswordSalt { get; set; }

        /// <summary>
        /// Gets or sets the last login date of the user
        /// </summary>
        public DateTime? LastLogin { get; set; }

        public static Player Create([NotNull] ICacheClient redis, [NotNull] string name, [NotNull] string username)
        {
            var newPlayer = Create<Player>(redis);
            newPlayer.Name = name;
            newPlayer.Username = username;
            return newPlayer;
        }

        /// <summary>
        /// Loads a <see cref="Player"/> from the cache or the data store
        /// </summary>
        /// <param name="redis">The client proxy to the underlying data store</param>
        /// <param name="playerRef">The <see cref="DbRef"/> of the <see cref="Player"/> to load</param>
        /// <returns>The <see cref="Player"/> if found; otherwise, null</returns>
        [NotNull, Pure, ItemCanBeNull]
        public static new async Task<Player> GetAsync([NotNull] ICacheClient redis, DbRef playerRef) => (Player)(await CacheManager.LookupOrRetrieveAsync(playerRef, redis, async d => await redis.GetAsync<Player>($"mudpie::player:{d}"))).DataObject;

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
            var p = obj as Player;
            // ReSharper disable once RedundantCast
            if ((object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return this.InternalId == p.InternalId;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.InternalId.GetHashCode();
        }

        /// <inheritdoc />
        public override async Task SaveAsync(ICacheClient redis)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            await
                Task.WhenAll(
                    redis.SetAddAsync<string>("mudpie::players", this.DbRef),
                    redis.AddAsync($"mudpie::player:{this.DbRef}", this),
                    redis.HashSetAsync("mudpie::usernames", this.Username.ToLowerInvariant(), this.DbRef),
                    CacheManager.UpdateAsync(this.DbRef, redis, this));
        }

        public void SetPassword([NotNull] SecureString password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            var saltBytes = new byte[64];
            var rng = RandomNumberGenerator.Create();
            Debug.Assert(rng != null, "rng != null");
            rng.GetNonZeroBytes(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);
            var bstr = Marshal.SecureStringToBSTR(password);
            try
            {
                this.PasswordHash = Convert.ToBase64String(new SHA512CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(string.Concat(salt, Marshal.PtrToStringBSTR(bstr)))));
                this.PasswordSalt = salt;
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
        }

        /// <summary>
        /// Verifies a password attempt against the hashed password stored for the user
        /// </summary>
        /// <param name="attempt">
        /// The password we are attempting to verify as the same
        /// </param>
        /// <returns>
        /// True if the password attempt provided matches the password hash for the player; otherwise, false.
        /// </returns>
        public bool VerifyPassword([NotNull] SecureString attempt)
        {
            var bstr = Marshal.SecureStringToBSTR(attempt);
            try
            {
                var attemptPasswordHash = Convert.ToBase64String(new SHA512CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(string.Concat(this.PasswordSalt, Marshal.PtrToStringBSTR(bstr)))));
                return string.Compare(attemptPasswordHash, this.PasswordHash, StringComparison.Ordinal) == 0;
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
        }
    }
}

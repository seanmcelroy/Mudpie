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
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A player is a general thing that represents the character owned and controlled by a real user
    /// </summary>
    public class Player : ObjectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Player"/> class.
        /// </summary>
        /// <param name="playerName">The name of the player</param>
        /// <param name="owner">The reference of the owner of the object</param>
        /// <param name="username">The identity of the user for authentication</param>
        public Player([NotNull] string playerName, DbRef owner, [NotNull] string username)
            : base(playerName, owner)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                throw new ArgumentNullException(nameof(playerName));
            }

            if (owner <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), owner, $"Owner must be set; value provided was {owner}");
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentNullException(nameof(username));
            }

            if (owner <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(owner), owner, $"Owner must be set; value provided was {owner}");
            }

            this.Username = username;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Player"/> class.
        /// </summary>
        [Obsolete("Only made public for a generic type parameter requirement", false)]

        // ReSharper disable once NotNullMemberIsNotInitialized
        public Player()
        {
        }

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

        /// <summary>
        /// Creates a new player with the specified vanity name and credential username
        /// </summary>
        /// <param name="redis">The client proxy to access the underlying data store</param>
        /// <param name="name">The display name of the new player</param>
        /// <param name="username">The login name of the new player</param>
        /// <returns>The newly-created <see cref="Player"/> object</returns>
        [NotNull, ItemNotNull]
        public static async Task<Player> CreateAsync([NotNull] ICacheClient redis, [NotNull] string name, [NotNull] string username)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentNullException(nameof(username));
            }

            var newPlayer = await CreateAsync<Player>(redis);
            newPlayer.Name = name;
            newPlayer.Username = username;
            return newPlayer;
        }

        /// <summary>
        /// Loads a <see cref="Player"/> from the cache or the data store
        /// </summary>
        /// <param name="redis">The client proxy to the underlying data store</param>
        /// <param name="playerRef">The <see cref="DbRef"/> of the <see cref="Player"/> to load</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>The <see cref="Player"/> if found; otherwise, null</returns>
        [NotNull, Pure, ItemCanBeNull]
        public static new async Task<Player> GetAsync([NotNull] ICacheClient redis, DbRef playerRef, CancellationToken cancellationToken) => (await CacheManager.LookupOrRetrieveAsync(playerRef, redis, async (d, token) => await redis.GetAsync<Player>($"mudpie::player:{d}"), cancellationToken))?.DataObject;

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
                    redis.SetAddAsync<string>("mudpie::players", this.DbRef),
                    redis.AddAsync($"mudpie::player:{this.DbRef}", this),
                    redis.HashSetAsync("mudpie::usernames", this.Username.ToLowerInvariant(), this.DbRef),
                    CacheManager.UpdateAsync(this.DbRef, redis, this, cancellationToken));
            // ReSharper restore PossibleNullReferenceException
        }

        /// <summary>
        /// Sets or changes the password of a player
        /// </summary>
        /// <param name="password">The new password for the player</param>
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
            if (attempt == null)
            {
                throw new ArgumentNullException(nameof(attempt));
            }

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

        /// <inheritdoc />
        public override void Sanitize()
        {
            this.LastLogin = null;
            this.PasswordHash = null;
            this.PasswordSalt = null;
            this.Username = null;

            base.Sanitize();
        }
    }
}

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Link.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A link is an "action" or "exit" that is contained within an object and points to another
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
    /// A link is an "action" or "exit" that is contained within an object and points to another
    /// </summary>
    public class Link : ObjectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Link"/> class.
        /// </summary>
        /// <param name="linkName">The name of the link</param>
        /// <param name="owner">The reference of the owner of the object</param>
        public Link([NotNull] string linkName, DbRef owner)
            : base(linkName, owner)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Link"/> class.
        /// </summary>
        [Obsolete("Only made public for a generic type parameter requirement", false)]
        public Link()
        {
        }

        /// <summary>
        /// Gets or sets the target of the link
        /// </summary>
        public DbRef Target { get; set; }

        /// <summary>
        /// Gets a <see cref="Link"/> from the underlying data store
        /// </summary>
        /// <param name="redis">The client proxy to access the underlying data store</param>
        /// <param name="linkRef">The <see cref="DbRef"/> of the link to retrieve from the data store</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>The matching <see cref="Link"/>, if it exists for the supplied <paramref name="linkRef"/>; otherwise, null.</returns>
        [NotNull, Pure, ItemCanBeNull]
        public static new async Task<Link> GetAsync([NotNull] ICacheClient redis, DbRef linkRef, CancellationToken cancellationToken) => (await CacheManager.LookupOrRetrieveAsync(linkRef, redis, async (d, token) => await redis.GetAsync<Link>($"mudpie::link:{d}"), cancellationToken))?.DataObject;

        /// <inheritdoc />
        public override async Task SaveAsync(ICacheClient redis, CancellationToken cancellationToken)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            await
                // ReSharper disable PossibleNullReferenceException
                Task.WhenAll(
                    redis.SetAddAsync<string>("mudpie::links", this.DbRef),
                    redis.AddAsync($"mudpie::link:{this.DbRef}", this),
                    CacheManager.UpdateAsync(this.DbRef, redis, this, cancellationToken));
            // ReSharper restore PossibleNullReferenceException
        }
    }
}

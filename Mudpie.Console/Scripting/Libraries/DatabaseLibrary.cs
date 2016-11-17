// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DatabaseLibrary.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A set of routines that allow a script to modify objects in the underlying data store
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Scripting.Libraries
{
    using System.Threading;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;
    using Server.Data;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A set of routines that allow a script to modify objects in the underlying data store
    /// </summary>
    public class DatabaseLibrary : IDatabaseLibrary
    {
        /// <summary>
        /// The object on which the verb that called the currently-running
        /// verb was found. For the first verb called for a given command, 'caller' has the
        /// same value as <see cref="Player"/>.
        /// </summary>
        private readonly DbRef caller;

        /// <summary>
        /// The client proxy to the underlying data store
        /// </summary>
        [NotNull]
        private readonly ICacheClient redis;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseLibrary"/> class.
        /// </summary>
        /// <param name="callerReference">The caller reference</param>
        /// <param name="redis">The client proxy to the underlying data store</param>
        public DatabaseLibrary(DbRef callerReference, [NotNull] ICacheClient redis)
        {
            this.caller = callerReference;
            this.redis = redis;
        }

        /// <inheritdoc />
        public bool Rename(DbRef reference, [NotNull] string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                return false;
            }

            var cts = new CancellationTokenSource(5000); // 5 seconds
            var getAsyncTask = ObjectBase.GetAsync(this.redis, reference, cts.Token); // 5 seconds
            if (!getAsyncTask.Wait(5000))
            {
                return false;
            }

            var target = getAsyncTask.Result;
            if (target == null)
            {
                return false;
            }

            if (!target.Owner.Equals(this.caller))
            {
                return false;
            }

            target.Name = newName;
            return target.SaveAsync(this.redis, cts.Token).Wait(5000);
        }
    }
}

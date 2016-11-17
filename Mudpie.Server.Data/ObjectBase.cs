// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ObjectBase.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   The base definition of any object in the MUD
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Server.Data
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using log4net;

    using Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// The base definition of any object in the MUD
    /// </summary>
    [PublicAPI]
    public abstract class ObjectBase : IObjectBase
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        [NotNull]
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ObjectBase));

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectBase"/> class.
        /// </summary>
        // ReSharper disable once NotNullMemberIsNotInitialized
        protected ObjectBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectBase"/> class.
        /// </summary>
        /// <param name="name">Name of the object</param>
        /// <param name="owner">The reference of the owner of the object</param>
        /// <exception cref="ArgumentNullException">Thrown if the name is null</exception>
        protected ObjectBase([NotNull] string name, DbRef owner)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (owner <= 0)
            {
                throw new ArgumentException($"Owner must be set; value provided was {owner}", nameof(owner));
            }

            this.Name = name;
            this.Owner = owner;
        }
        
        /// <summary>
        /// Gets or sets the database reference of the object
        /// </summary>
        public DbRef DbRef { get; set; } = -1;

        /// <summary>
        /// Gets or sets the name of the object
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the aliases of this object
        /// </summary>
        public string[] Aliases { get; set; }

        /// <summary>
        /// Gets or sets the owner of the object
        /// </summary>
        public DbRef Owner { get; set; }

        /// <summary>
        /// Gets or sets the description of the object if a user were to observe it directly
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the location of this object
        /// </summary>
        public DbRef Location { get; set; } = DbRef.Nothing;

        /// <summary>
        /// Gets or sets the inverse of 'location', the contents of this object
        /// </summary>
        public DbRef[] Contents { get; set; }

        /// <summary>
        /// Gets or sets the parent of this object, from which it inherits properties and verbs
        /// </summary>
        public DbRef Parent { get; set; } = DbRef.Nothing;

        [NotNull, ItemNotNull]
        public static async Task<T> CreateAsync<T>([NotNull] ICacheClient redis) where T : ObjectBase, new()
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            var newObject = new T
                                {
                                    DbRef = Convert.ToInt32(await redis.Database.StringIncrementAsync("mudpie::dbref:counter"))
                                };

            return newObject;
        }

        [NotNull, Pure]
        public static async Task<bool> ExistsAsync([NotNull] ICacheClient redis, DbRef reference)
        {
            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            var referenceString = "\"" + (string)reference + "\"";

            var tasks = new[]
                            {
                                redis.Database.SetContainsAsync("mudpie::links", referenceString),
                                redis.Database.SetContainsAsync("mudpie::players", referenceString),
                                redis.Database.SetContainsAsync("mudpie::programs", referenceString),
                                redis.Database.SetContainsAsync("mudpie::rooms", referenceString)
                            };

            await Task.WhenAll(tasks);

            return tasks.Any(t => t.Result);
        }

        [NotNull, Pure, ItemCanBeNull]
        public static async Task<ObjectBase> GetAsync([NotNull] ICacheClient redis, DbRef reference, CancellationToken cancellationToken)
        {
            var referenceString = "\"" + (string)reference + "\"";

            var tasks = new[]
                            {
                                redis.Database.SetContainsAsync("mudpie::links", referenceString),
                                redis.Database.SetContainsAsync("mudpie::players", referenceString),
                                redis.Database.SetContainsAsync("mudpie::programs", referenceString),
                                redis.Database.SetContainsAsync("mudpie::rooms", referenceString)
                            };

            await Task.WhenAll(tasks);

            Debug.Assert(tasks != null, "tasks!= null");
            Debug.Assert(tasks[0] != null, "tasks[0] != null)");
            Debug.Assert(tasks[1] != null, "tasks[1] != null");
            Debug.Assert(tasks[2] != null, "tasks[2] != null");
            Debug.Assert(tasks[3] != null, "tasks[3] != null");

            if (tasks[0].Result)
            {
                return await Link.GetAsync(redis, reference, cancellationToken);
            }

            if (tasks[1].Result)
            {
                return await Player.GetAsync(redis, reference, cancellationToken);
            }

            if (tasks[2].Result)
            {
                return await Program.GetAsync(redis, reference, cancellationToken);
            }

            if (tasks[3].Result)
            {
                return await Room.GetAsync(redis, reference, cancellationToken);
            }

            Logger.Warn($"Unable to resolve DbRef {reference}");
            return null;
        }

        /// <inheritdoc />
        public async Task MoveAsync(DbRef newLocation, ICacheClient redis, CancellationToken cancellationToken)
        {
            Debug.Assert(!newLocation.Equals(DbRef.Nothing), "!newLocation.Equals(DbRef.NOTHING)");
            Debug.Assert(!newLocation.Equals(DbRef.Ambiguous), "!newLocation.Equals(DbRef.AMBIGUOUS)");
            Debug.Assert(!newLocation.Equals(DbRef.FailedMatch), "!newLocation.Equals(DbRef.FAILED_MATCH)");

            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            if (this.Location.Equals(newLocation))
            {
                return;
            }

            var oldLocationObject = this.Location.Equals(DbRef.Nothing) ? null : await CacheManager.LookupOrRetrieveAsync(this.Location, redis, async (d, token) => await GetAsync(redis, d, token), cancellationToken);
            var newLocationObject = await CacheManager.LookupOrRetrieveAsync(newLocation, redis, async (d, token) => await GetAsync(redis, d, token), cancellationToken);

            if (newLocationObject != null)
            {
                if (oldLocationObject != null)
                {
                    oldLocationObject.DataObject.RemoveContents(this.DbRef);
                    await oldLocationObject.DataObject.SaveAsync(redis, cancellationToken);
                }

                newLocationObject.DataObject.AddContents(this.DbRef);
                await newLocationObject.DataObject.SaveAsync(redis, cancellationToken);

                this.Location = newLocation;
                await this.SaveAsync(redis, cancellationToken);

                await CacheManager.UpdateAsync(newLocation, redis, newLocationObject.DataObject, cancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task ReparentAsync(DbRef newParent, ICacheClient redis, CancellationToken cancellationToken)
        {
            Debug.Assert(!newParent.Equals(DbRef.Nothing), "!newParent.Equals(DbRef.NOTHING)");
            Debug.Assert(!newParent.Equals(DbRef.Ambiguous), "!newParent.Equals(DbRef.AMBIGUOUS)");
            Debug.Assert(!newParent.Equals(DbRef.FailedMatch), "!newParent.Equals(DbRef.FAILED_MATCH)");

            if (redis == null)
            {
                throw new ArgumentNullException(nameof(redis));
            }

            var newParentObject = await CacheManager.LookupOrRetrieveAsync(newParent, redis, async (d, token) => await GetAsync(redis, d, token), cancellationToken);

            if (newParentObject != null)
            {
                this.Parent = newParent;
                await this.SaveAsync(redis, cancellationToken);
            }
        }

        /// <summary>
        /// Saves the object back to the persistent data store
        /// </summary>
        /// <param name="redis">The client proxy to access the underlying data store</param>
        /// <param name="cancellationToken">A cancellation token used to abort the method</param>
        /// <returns>A task object used to await this method for completion</returns>
        public abstract Task SaveAsync(ICacheClient redis, CancellationToken cancellationToken);

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{this.Name}({this.DbRef})";
        }

        private void AddContents(params DbRef[] references)
        {
            if (references != null)
            {
                foreach (var reference in references)
                {
                    Debug.Assert(!reference.Equals(DbRef.Nothing), "!reference.Equals(DbRef.NOTHING)");
                    Debug.Assert(!reference.Equals(DbRef.Ambiguous), "!reference.Equals(DbRef.AMBIGUOUS)");
                    Debug.Assert(!reference.Equals(DbRef.FailedMatch), "!reference.Equals(DbRef.FAILED_MATCH)");

                    if (this.Contents == null)
                    {
                        this.Contents = new[] { reference };
                    }
                    else
                    {
                        var contents = new List<DbRef>(this.Contents) { reference };
                        this.Contents = contents.ToArray();
                    }
                }
            }
        }

        private void RemoveContents(params DbRef[] references)
        {
            if (references != null)
            {
                foreach (var reference in references)
                {
                    Debug.Assert(!reference.Equals(DbRef.Nothing), "!reference.Equals(DbRef.NOTHING)");
                    Debug.Assert(!reference.Equals(DbRef.Ambiguous), "!reference.Equals(DbRef.AMBIGUOUS)");
                    Debug.Assert(!reference.Equals(DbRef.FailedMatch), "!reference.Equals(DbRef.FAILED_MATCH)");

                    if (this.Contents == null)
                    {
                        return;
                    }

                    var contents = new List<DbRef>(this.Contents);
                    contents.Remove(reference);
                    this.Contents = contents.ToArray();
                }
            }
        }
    }
}

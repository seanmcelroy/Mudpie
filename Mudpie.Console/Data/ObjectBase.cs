// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ObjectBase.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   The base definition of any object in the MUD
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console.Data
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// The base definition of any object in the MUD
    /// </summary>
    [PublicAPI]
    public abstract class ObjectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectBase"/> class.
        /// </summary>
        protected ObjectBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectBase"/> class.
        /// </summary>
        /// <param name="name">Name of the object</param>
        /// <exception cref="ArgumentNullException">Thrown if the name is null</exception>
        protected ObjectBase([NotNull] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            this.Name = name;
        }

        /// <summary>
        /// Gets or sets the database reference of the object
        /// </summary>
        public DbRef DbRef { get; set; } = -1;

        /// <summary>
        /// Gets or sets the globally unique identifier of the object
        /// </summary>
        /// <remarks>
        /// This exists separate from a DBREF to allow for synchronization scenarios in the future
        /// </remarks>
        [NotNull]
        public string InternalId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets or sets the name of the object
        /// </summary>
        [NotNull]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the aliases of this object
        /// </summary>
        [CanBeNull]
        public string[] Aliases { get; set; }

        /// <summary>
        /// Gets or sets the description of the object if a user were to observe it directly
        /// </summary>
        [CanBeNull]
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the location of this object
        /// </summary>
        public DbRef Location { get; set; } = DbRef.NOTHING;

        /// <summary>
        /// Gets or sets the inverse of 'location', the contents of this object
        /// </summary>
        [CanBeNull]
        public DbRef[] Contents { get; set; }

        /// <summary>
        /// Gets or sets the parent of this object, from which it inherits properties and verbs
        /// </summary>
        public DbRef Parent { get; set; } = DbRef.NOTHING;

        [NotNull]
        public static T Create<T>([NotNull] ICacheClient redis) where T : ObjectBase, new()
        {
            if (redis == null)
                throw new ArgumentNullException(nameof(redis));

            var newObject = new T
                                {
                                    DbRef = Convert.ToInt32(redis.Database.StringIncrement("mudpie::dbref:counter"))
                                };

            return newObject;
        }

        [NotNull, Pure]
        public static async Task<bool> ExistsAsync([NotNull] ICacheClient redis, DbRef reference)
        {
            if (redis == null)
                throw new ArgumentNullException(nameof(redis));

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
        public static async Task<ObjectBase> GetAsync([NotNull] ICacheClient redis, DbRef reference)
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
                return await Link.GetAsync(redis, reference);
            }

            if (tasks[1].Result)
            {
                return await Player.GetAsync(redis, reference);
            }

            if (tasks[2].Result)
            {
                return await Program.GetAsync(redis, reference);
            }

            if (tasks[3].Result)
            {
                return await Room.GetAsync(redis, reference);
            }

            throw new NotImplementedException();
        }

        public void AddContents(params DbRef[] references)
        {
            if (references != null)
                foreach (var reference in references)
                {
                    Debug.Assert(!reference.Equals(DbRef.NOTHING), "!reference.Equals(DbRef.NOTHING)");
                    Debug.Assert(!reference.Equals(DbRef.AMBIGUOUS), "!reference.Equals(DbRef.AMBIGUOUS)");
                    Debug.Assert(!reference.Equals(DbRef.FAILED_MATCH), "!reference.Equals(DbRef.FAILED_MATCH)");

                    if (this.Contents == null)
                        this.Contents = new[] { reference };
                    else
                    {
                        var contents = new List<DbRef>(this.Contents)
                                           {
                                               reference
                                           };
                        this.Contents = contents.ToArray();
                    }
                }
        }

        public void RemoveContents(params DbRef[] references)
        {
            if (references != null)
                foreach (var reference in references)
                {
                    Debug.Assert(!reference.Equals(DbRef.NOTHING), "!reference.Equals(DbRef.NOTHING)");
                    Debug.Assert(!reference.Equals(DbRef.AMBIGUOUS), "!reference.Equals(DbRef.AMBIGUOUS)");
                    Debug.Assert(!reference.Equals(DbRef.FAILED_MATCH), "!reference.Equals(DbRef.FAILED_MATCH)");

                    if (this.Contents == null)
                        return;
                    else
                    {
                        var contents = new List<DbRef>(this.Contents);
                        contents.Remove(reference);
                        this.Contents = contents.ToArray();
                    }
                }
        }

        public async Task ReparentAsync(DbRef newParent, [NotNull] ICacheClient redis)
        {
            Debug.Assert(!newParent.Equals(DbRef.NOTHING), "!newParent.Equals(DbRef.NOTHING)");
            Debug.Assert(!newParent.Equals(DbRef.AMBIGUOUS), "!newParent.Equals(DbRef.AMBIGUOUS)");
            Debug.Assert(!newParent.Equals(DbRef.FAILED_MATCH), "!newParent.Equals(DbRef.FAILED_MATCH)");
            
            var oldParentObject = this.Parent.Equals(DbRef.NOTHING) ? null : await CacheManager.LookupOrRetrieveAsync(this.Parent, redis, async d => await GetAsync(redis, d));
            var newParentObject = await CacheManager.LookupOrRetrieveAsync(newParent, redis, async d => await GetAsync(redis, d));

            if (newParentObject != null)
            {
                if (oldParentObject != null)
                {
                    oldParentObject.DataObject.RemoveContents(this.DbRef);
                    await oldParentObject.DataObject.SaveAsync(redis);
                }

                newParentObject.DataObject.AddContents(this.DbRef);
                await newParentObject.DataObject.SaveAsync(redis);

                this.Parent = newParent;
                await this.SaveAsync(redis);
            }
        }

        /// <summary>
        /// Saves the object back to the persistent data store
        /// </summary>
        /// <param name="redis">The client proxy to access the underlying data store</param>
        /// <returns>A task object used to await this method for completion</returns>
        [NotNull]
        public abstract Task SaveAsync([NotNull] ICacheClient redis);
    }
}

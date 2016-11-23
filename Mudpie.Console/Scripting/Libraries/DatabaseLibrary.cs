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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using JetBrains.Annotations;

    using log4net;

    using Mudpie.Scripting.Common;
    using Server.Data;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A set of routines that allow a script to modify objects in the underlying data store
    /// </summary>
    public class DatabaseLibrary : IDatabaseLibrary
    {
        /// <summary>
        /// The logging utility instance to use to log events from this class
        /// </summary>
        [NotNull]

        // ReSharper disable once AssignNullToNotNullAttribute
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ObjectBase));

        /// <summary>
        /// The object on which the verb that called the currently-running
        /// verb was found. For the first verb called for a given command, 'caller' has the
        /// same value as <see cref="Player"/>.
        /// </summary>
        [NotNull]
        private readonly ObjectBase caller;

        /// <summary>
        /// The client proxy to the underlying data store
        /// </summary>
        [NotNull]
        private readonly ICacheClient redis;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseLibrary"/> class.
        /// </summary>
        /// <param name="caller">The caller of the functions in this library</param>
        /// <param name="redis">The client proxy to the underlying data store</param>
        public DatabaseLibrary([NotNull] ObjectBase caller, [NotNull] ICacheClient redis)
        {
            this.caller = caller;
            this.redis = redis;
        }

        /// <inheritdoc />
        public DbRef CreateRoom(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return DbRef.Nothing;
            }

            var cts = new CancellationTokenSource(5000); // 5 seconds
            var locationGetAsyncTask = ObjectBase.GetAsync(this.redis, this.caller.Location, cts.Token);
            if (!locationGetAsyncTask.Wait(5000))
            {
                return DbRef.Nothing;
            }

            var composedCallerLocation = locationGetAsyncTask.Result;
            if (composedCallerLocation == null)
            {
                return DbRef.Nothing;
            }

            var createRoomAsyncTask = Room.CreateAsync(this.redis, name);
            if (!createRoomAsyncTask.Wait(5000))
            {
                return DbRef.Nothing;
            }

            var room = createRoomAsyncTask.Result;
            room.Location = composedCallerLocation.Location;
            room.Owner = this.caller.Owner;
            room.Parent = composedCallerLocation.Parent;
            room.SaveAsync(this.redis, cts.Token).Wait(cts.Token);

            Logger.Info($"Object {this.caller} created new room {name}({room.DbRef})");
            return room.DbRef;
        }

        /// <inheritdoc />
        public bool Rename(DbRef reference, string newName)
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

            if (!target.Owner.Equals(this.caller.DbRef))
            {
                return false;
            }

            target.Name = newName;
            var saveAsyncTask = target.SaveAsync(this.redis, cts.Token);
            if (!saveAsyncTask.Wait(5000))
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public bool SetProperty(DbRef reference, string propertyName, object propertyValue)
        {
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

            var existingProperty = target.Properties?.FirstOrDefault(p => p != null && string.Compare(p.Name, propertyName, StringComparison.OrdinalIgnoreCase) == 0);
            if (existingProperty == null)
            {
                // If unset and property to set to is null, don't do anything.
                if (propertyValue == null || DbRef.Nothing.Equals(propertyValue))
                {
                    return false;
                }

                // TODO: We currently let anyone create an unset property on any other object.  That should change, but how.
                var newProperty = new Property
                                      {
                                          Name = propertyName,
                                          Owner = this.caller.DbRef,
                                          Value = propertyValue
                                      };

                target.Properties = target.Properties == null ? new[] { newProperty } : (new List<Property>(target.Properties) { newProperty }).ToArray();
                var saveAsyncTask = target.SaveAsync(this.redis, cts.Token);
                if (!saveAsyncTask.Wait(5000))
                {
                    return false;
                }

                return true;
            }

            // Property does exist

            // If I am not the owner, then to change this property, it must be publically writable
            if (!target.Owner.Equals(this.caller.DbRef))
            {
                return false;
            }
            
            // TODO....
            throw new NotImplementedException();
        }
    }
}

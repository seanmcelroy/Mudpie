// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MatchUtility.cs" company="Sean McElroy">
//   Released under the terms of the MIT License
// </copyright>
// <summary>
//   A series of utility methods for finding objects a player references in their commands
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Mudpie.Console
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;
    using Mudpie.Server.Data;

    using StackExchange.Redis.Extensions.Core;

    /// <summary>
    /// A series of utility methods for finding objects a player references in their commands
    /// </summary>
    public static class MatchUtility
    {
        /// <summary>
        /// Matches a verb to actions in the user's reachable environment
        /// </summary>
        /// <param name="player">The player who entered the verb</param>
        /// <param name="redis">The client proxy to the underlying data store</param>
        /// <param name="text">The verb the player entered</param>
        /// <param name="directObjectRef">The <see cref="DbRef"/> of the matched direct object</param>
        /// <param name="indirectObjectRef">The <see cref="DbRef"/> of the matched indirect object</param>
        /// <returns>The <see cref="DbRef"/> of the action/link, if it could be located</returns>
        public static async Task<Tuple<DbRef, ObjectBase>> MatchVerbAsync([CanBeNull] Player player, [NotNull] ICacheClient redis, [NotNull] string text, DbRef directObjectRef, DbRef indirectObjectRef)
        {
            var matched = await MatchTypeAsync<Link>(player, redis, text);

            if (!matched.Item1.Equals(DbRef.FAILED_MATCH))
            {
                return matched;
            }

            // We didn't find a verb, so try hunting on the Direct Object
            if (!directObjectRef.Equals(DbRef.AMBIGUOUS) && !directObjectRef.Equals(DbRef.FAILED_MATCH) && !directObjectRef.Equals(DbRef.NOTHING))
            {
                var exactMatch = DbRef.FAILED_MATCH;
                ObjectBase lastExactMatchObject = null;
                var partialMatch = DbRef.FAILED_MATCH;
                ObjectBase lastPartialMatchObject = null;

                var directObject = await CacheManager.LookupOrRetrieveAsync(directObjectRef, redis, async d => await ObjectBase.GetAsync(redis, d));
                MatchTypeOnObject<Link>(text, directObject, ref exactMatch, ref lastExactMatchObject, ref partialMatch, ref lastPartialMatchObject);

                if (exactMatch > 0)
                {
                    return exactMatch > 0
                        ? new Tuple<DbRef, ObjectBase>(exactMatch, lastExactMatchObject)
                        : new Tuple<DbRef, ObjectBase>(exactMatch, null);
                }

                if (partialMatch > 0)
                {
                    return partialMatch > 0
                        ? new Tuple<DbRef, ObjectBase>(partialMatch, lastPartialMatchObject)
                        : new Tuple<DbRef, ObjectBase>(partialMatch, null);
                }

                return new Tuple<DbRef, ObjectBase>(exactMatch + partialMatch, null);
            }

            // We didn't find a verb, so try hunting on the Indirect Object
            if (!indirectObjectRef.Equals(DbRef.AMBIGUOUS) && !indirectObjectRef.Equals(DbRef.FAILED_MATCH) && !indirectObjectRef.Equals(DbRef.NOTHING))
            {
                var exactMatch = DbRef.FAILED_MATCH;
                ObjectBase lastExactMatchObject = null;
                var partialMatch = DbRef.FAILED_MATCH;
                ObjectBase lastPartialMatchObject = null;

                var indirectObject = await CacheManager.LookupOrRetrieveAsync(indirectObjectRef, redis, async d => await ObjectBase.GetAsync(redis, d));
                MatchTypeOnObject<Link>(text, indirectObject, ref exactMatch, ref lastExactMatchObject, ref partialMatch, ref lastPartialMatchObject);

                if (exactMatch > 0)
                {
                    return exactMatch > 0
                        ? new Tuple<DbRef, ObjectBase>(exactMatch, lastExactMatchObject)
                        : new Tuple<DbRef, ObjectBase>(exactMatch, null);
                }

                if (partialMatch > 0)
                {
                    return partialMatch > 0
                        ? new Tuple<DbRef, ObjectBase>(partialMatch, lastPartialMatchObject)
                        : new Tuple<DbRef, ObjectBase>(partialMatch, null);
                }

                return new Tuple<DbRef, ObjectBase>(exactMatch + partialMatch, null);
            }

            return new Tuple<DbRef, ObjectBase>(DbRef.FAILED_MATCH, null);
        }

        /// <summary>
        /// Matches a direct or indirect object to items in the user's reachable environment
        /// </summary>
        /// <param name="player">The player who entered the object name</param>
        /// <param name="redis">The client proxy to the underlying data store</param>
        /// <param name="text">The object name the player entered</param>
        /// <returns>The <see cref="DbRef"/> of the object and the <see cref="ObjectBase"/> representation of it, if it could be located</returns>
        public static async Task<Tuple<DbRef, ObjectBase>> MatchObjectAsync([CanBeNull] Player player, [NotNull] ICacheClient redis, [CanBeNull] string text)
        {
            // Did they provide a DbRef?
            DbRef reference;
            if (DbRef.TryParse(text, out reference))
            {
                // Only return the reference if the object exists.
                var lookup = await ObjectBase.GetAsync(redis, reference);
                return lookup == null
                    ? new Tuple<DbRef, ObjectBase>(DbRef.FAILED_MATCH, null)
                    : new Tuple<DbRef, ObjectBase>(reference, lookup);
            }

            // Is it a special pronoun?
            if (string.Compare(text, "me", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return player == null
                    ? new Tuple<DbRef, ObjectBase>(DbRef.FAILED_MATCH, null)
                    : new Tuple<DbRef, ObjectBase>(player.DbRef, player);
            }

            if (string.Compare(text, "here", StringComparison.InvariantCultureIgnoreCase) == 0)
            {
                return player == null || player.Location.Equals(DbRef.NOTHING)
                    ? new Tuple<DbRef, ObjectBase>(DbRef.FAILED_MATCH, null)
                    : new Tuple<DbRef, ObjectBase>(player.Location, await ObjectBase.GetAsync(redis, player.Location));
            }

            return await MatchTypeAsync<ObjectBase>(player, redis, text);
        }

        /// <summary>
        /// Matches any object to items in the user's reachable environment
        /// </summary>
        /// <typeparam name="T">
        /// The type of <see cref="ObjectBase"/> to filter results by
        /// </typeparam>
        /// <param name="player">
        /// The player who entered the object name
        /// </param>
        /// <param name="redis">
        /// The client proxy to the underlying data store
        /// </param>
        /// <param name="text">
        /// The object name the player entered
        /// </param>
        /// <returns>
        /// The <see cref="DbRef"/> of the object, if it could be located
        /// </returns>
        private static async Task<Tuple<DbRef, ObjectBase>> MatchTypeAsync<T>([CanBeNull] Player player, [NotNull] ICacheClient redis, [CanBeNull] string text)
            where T : ObjectBase
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new Tuple<DbRef, ObjectBase>(DbRef.NOTHING, null);
            }

            var exactMatch = DbRef.FAILED_MATCH;
            ObjectBase lastExactMatchObject = null;
            var partialMatch = DbRef.FAILED_MATCH;
            ObjectBase lastPartialMatchObject = null;

            // Places to check - #1 - The player who typed the command
            if (player?.Contents != null)
            {
                foreach (var playerItem in player.Contents)
                {
                    var playerItemObject = await CacheManager.LookupOrRetrieveAsync(playerItem, redis, async d => await ObjectBase.GetAsync(redis, d));
                    MatchTypeOnObject<T>(text, playerItemObject, ref exactMatch, ref lastExactMatchObject, ref partialMatch, ref lastPartialMatchObject);
                }
            }

            // Places to check - #2 - The room the player is in
            if (player != null)
            {
                var playerLocationObject = await CacheManager.LookupOrRetrieveAsync(player.Location, redis, async d => await ObjectBase.GetAsync(redis, d));
                if (playerLocationObject?.Contents != null)
                {
                    foreach (var roomItemObject in playerLocationObject.Contents)
                    {
                        MatchTypeOnObject<T>(text, roomItemObject, ref exactMatch, ref lastExactMatchObject, ref partialMatch, ref lastPartialMatchObject);
                    }
                }
            }

            if (exactMatch > 0)
            {
                return exactMatch > 0
                    ? new Tuple<DbRef, ObjectBase>(exactMatch, lastExactMatchObject)
                    : new Tuple<DbRef, ObjectBase>(exactMatch, null);
            }

            if (partialMatch > 0)
            {
                return partialMatch > 0
                    ? new Tuple<DbRef, ObjectBase>(partialMatch, lastPartialMatchObject)
                    : new Tuple<DbRef, ObjectBase>(partialMatch, null);
            }

            return new Tuple<DbRef, ObjectBase>(exactMatch + partialMatch, null);
        }

        private static void MatchTypeOnObject<T>(
            [NotNull] string text,
            ComposedObject searchObject,
            ref DbRef exactMatch,
            ref ObjectBase lastExactMatchObject,
            ref DbRef partialMatch,
            ref ObjectBase lastPartialMatchObject)
        {
            if (!(searchObject?.DataObject is T))
            {
                return;
            }

            if (string.Compare(searchObject.DataObject.Name, text, StringComparison.OrdinalIgnoreCase) == 0)
            {
                exactMatch += searchObject.DataObject.DbRef;
                lastExactMatchObject = searchObject.DataObject;
            }
            else if (searchObject.DataObject.Aliases != null && searchObject.DataObject.Aliases.Any(a => string.Compare(a, text, StringComparison.OrdinalIgnoreCase) == 0))
            {
                exactMatch += searchObject.DataObject.DbRef;
                lastExactMatchObject = searchObject.DataObject;
            }
            else if (Regex.IsMatch(text, searchObject.DataObject.Name.Replace("*", ".*?"), RegexOptions.IgnoreCase))
            {
                partialMatch += searchObject.DataObject.DbRef;
                lastPartialMatchObject = searchObject.DataObject;
            }
            else if (searchObject.DataObject.Aliases != null
                     && searchObject.DataObject.Aliases.Any(
                         a => Regex.IsMatch(text, searchObject.DataObject.Name.Replace("*", ".*?"), RegexOptions.IgnoreCase)))
            {
                partialMatch += searchObject.DataObject.DbRef;
                lastPartialMatchObject = searchObject.DataObject;
            }
        }
    }
}

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

    using Data;

    using JetBrains.Annotations;

    using Mudpie.Scripting.Common;

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
        /// <returns>The <see cref="DbRef"/> of the action/link, if it could be located</returns>
        public static async Task<DbRef> MatchVerbAsync([CanBeNull] Player player, [NotNull] ICacheClient redis, [NotNull] string text, DbRef directObjectRef, DbRef indirectObjectRef)
        {
            var matched = await MatchTypeAsync<Link>(player, redis, text);

            if (!matched.Equals(DbRef.FAILED_MATCH))
                return matched;

            // We didn't find a verb, so try hunting on the Direct Object
            if (!directObjectRef.Equals(DbRef.AMBIGUOUS) && !directObjectRef.Equals(DbRef.FAILED_MATCH) && !directObjectRef.Equals(DbRef.NOTHING))
            {
                var exactMatch = DbRef.FAILED_MATCH;
                var partialMatch = DbRef.FAILED_MATCH;

                var directObject = await CacheManager.LookupOrRetrieveAsync(directObjectRef, redis, async d => await ObjectBase.GetAsync(redis, d));
                MatchTypeOnObject<Link>(redis, text, directObject, ref exactMatch, ref partialMatch);
                return !exactMatch.Equals(DbRef.AMBIGUOUS) && !exactMatch.Equals(DbRef.FAILED_MATCH) ? exactMatch : partialMatch;
            }

            // We didn't find a verb, so try hunting on the Indirect Object
            if (!indirectObjectRef.Equals(DbRef.AMBIGUOUS) && !indirectObjectRef.Equals(DbRef.FAILED_MATCH) && !indirectObjectRef.Equals(DbRef.NOTHING))
            {
                var exactMatch = DbRef.FAILED_MATCH;
                var partialMatch = DbRef.FAILED_MATCH;

                var indirectObject = await CacheManager.LookupOrRetrieveAsync(indirectObjectRef, redis, async d => await ObjectBase.GetAsync(redis, d));
                MatchTypeOnObject<Link>(redis, text, indirectObject, ref exactMatch, ref partialMatch);
                return !exactMatch.Equals(DbRef.AMBIGUOUS) && !exactMatch.Equals(DbRef.FAILED_MATCH) ? exactMatch : partialMatch;
            }

            return DbRef.FAILED_MATCH;
        }

        /// <summary>
        /// Matches a direct or indirect object to items in the user's reachable environment
        /// </summary>
        /// <param name="player">The player who entered the object name</param>
        /// <param name="redis">The client proxy to the underlying data store</param>
        /// <param name="text">The object name the player entered</param>
        /// <returns>The <see cref="DbRef"/> of the object, if it could be located</returns>
        public static async Task<DbRef> MatchObjectAsync([CanBeNull] Player player, [NotNull] ICacheClient redis, [CanBeNull] string text)
        {
            // Did they provide a DbRef?
            DbRef reference;
            if (DbRef.TryParse(text, out reference))
            {
                // Only return the reference if the object exists.
                if (await ObjectBase.ExistsAsync(redis, reference))
                    return reference;
                return DbRef.FAILED_MATCH;
            }

            // Is it a special pronoun?
            if (string.Compare(text, "me", StringComparison.InvariantCultureIgnoreCase) == 0)
                return player?.DbRef ?? DbRef.NOTHING;
            if (string.Compare(text, "here", StringComparison.InvariantCultureIgnoreCase) == 0)
                return player?.Location ?? DbRef.NOTHING;

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
        private static async Task<DbRef> MatchTypeAsync<T>([CanBeNull] Player player, [NotNull] ICacheClient redis, [CanBeNull] string text)
            where T : ObjectBase
        {
            if (string.IsNullOrWhiteSpace(text))
                return DbRef.NOTHING;

            var exactMatch = DbRef.FAILED_MATCH;
            var partialMatch = DbRef.FAILED_MATCH;

            // Places to check - #1 - The player who typed the command
            if (player?.Contents != null)
                foreach (var playerItem in player.Contents)
                {
                    var playerItemObject = await CacheManager.LookupOrRetrieveAsync(playerItem, redis, async d => await ObjectBase.GetAsync(redis, d));
                    MatchTypeOnObject<T>(redis, text, playerItemObject, ref exactMatch, ref partialMatch);
                }

            // Places to check - #2 - The room the player is in
            if (player != null)
            {
                var playerLocationObject = await CacheManager.LookupOrRetrieveAsync(player.Location, redis, async d => await ObjectBase.GetAsync(redis, d));
                if (playerLocationObject?.Contents != null)
                    foreach (var roomItemObject in playerLocationObject.Contents)
                        MatchTypeOnObject<T>(redis, text, roomItemObject, ref exactMatch, ref partialMatch);
            }

            return !exactMatch.Equals(DbRef.AMBIGUOUS) && !exactMatch.Equals(DbRef.FAILED_MATCH) ? exactMatch : partialMatch;
        }
        
        private static void MatchTypeOnObject<T>([NotNull] ICacheClient redis, [NotNull] string text, ComposedObject searchObject, ref DbRef exactMatch, ref DbRef partialMatch)
        {
            if (!(searchObject?.DataObject is T))
                return;
            if (string.Compare(searchObject.DataObject.Name, text, StringComparison.OrdinalIgnoreCase) == 0)
                exactMatch += searchObject.DataObject.DbRef;
            else if (searchObject.DataObject.Aliases != null && searchObject.DataObject.Aliases.Any(a => string.Compare(a, text, StringComparison.OrdinalIgnoreCase) == 0))
                exactMatch += searchObject.DataObject.DbRef;
            else if (Regex.IsMatch(text, searchObject.DataObject.Name.Replace("*", ".*?"), RegexOptions.IgnoreCase))
                partialMatch += searchObject.DataObject.DbRef;
            else if (searchObject.DataObject.Aliases != null && searchObject.DataObject.Aliases.Any(a => Regex.IsMatch(text, searchObject.DataObject.Name.Replace("*", ".*?"), RegexOptions.IgnoreCase)))
                partialMatch += searchObject.DataObject.DbRef;
        }
    }
}

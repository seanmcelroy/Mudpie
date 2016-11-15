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

    using Mudpie.Console.Data;
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
        public static async Task<DbRef> MatchVerbAsync([CanBeNull] Player player, [NotNull] ICacheClient redis, [CanBeNull] string text)
        {
            return await MatchTypeAsync<Link>(player, redis, text);
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
            var paritalMatch = DbRef.FAILED_MATCH;

            // Places to check - #1 - The player who typed the command
            if (player?.Contents != null)
                foreach (var playerItem in player.Contents)
                {
                    var playerItemObject = await CacheManager.LookupOrRetrieveAsync(playerItem, redis, async d => await ObjectBase.GetAsync(redis, d));
                    if (playerItemObject?.DataObject is T)
                    {
                        if (string.Compare(playerItemObject.DataObject.Name, text, StringComparison.OrdinalIgnoreCase) == 0)
                            exactMatch += playerItemObject.DataObject.DbRef;
                        else if (playerItemObject.DataObject.Aliases != null && playerItemObject.DataObject.Aliases.Any(a => string.Compare(a, text, StringComparison.OrdinalIgnoreCase) == 0))
                            exactMatch += playerItemObject.DataObject.DbRef;
                        else if (Regex.IsMatch(text, playerItemObject.DataObject.Name.Replace("*", ".*?"), RegexOptions.IgnoreCase))
                            paritalMatch += playerItemObject.DataObject.DbRef;
                        else if (playerItemObject.DataObject.Aliases != null && playerItemObject.DataObject.Aliases.Any(a => Regex.IsMatch(text, playerItemObject.DataObject.Name.Replace("*", ".*?"), RegexOptions.IgnoreCase)))
                            paritalMatch += playerItemObject.DataObject.DbRef;
                    }
                }

            // Places to check - #2 - The room the player is in
            if (player != null)
            {
                var playerLocationObject = await CacheManager.LookupOrRetrieveAsync(player.Location, redis, async d => await ObjectBase.GetAsync(redis, d));
                if (playerLocationObject?.Contents != null)
                    foreach (var roomItemObject in playerLocationObject.Contents)
                    {
                        if (roomItemObject?.DataObject is T)
                        {
                            if (string.Compare(roomItemObject.DataObject.Name, text, StringComparison.OrdinalIgnoreCase) == 0)
                                exactMatch += roomItemObject.DataObject.DbRef;
                            else if (roomItemObject.DataObject.Aliases != null && roomItemObject.DataObject.Aliases.Any(a => string.Compare(a, text, StringComparison.OrdinalIgnoreCase) == 0))
                                exactMatch += roomItemObject.DataObject.DbRef;
                            else if (Regex.IsMatch(text, roomItemObject.DataObject.Name.Replace("*", ".*?"), RegexOptions.IgnoreCase))
                                paritalMatch += roomItemObject.DataObject.DbRef;
                            else if (roomItemObject.DataObject.Aliases != null && roomItemObject.DataObject.Aliases.Any(a => Regex.IsMatch(text, roomItemObject.DataObject.Name.Replace("*", ".*?"), RegexOptions.IgnoreCase)))
                                paritalMatch += roomItemObject.DataObject.DbRef;
                        }
                    }
            }

            return !exactMatch.Equals(DbRef.AMBIGUOUS) && !exactMatch.Equals(DbRef.FAILED_MATCH) ? exactMatch : paritalMatch;
        }
    }
}
